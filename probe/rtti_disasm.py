# 反汇编 GamingBoxHost.exe 的 EnableTouchpad / DisableTouchpad（inventec_sync_operation）成员函数，
# 提取它们调用的导入函数（IAT），确认触控板启用的真实系统通道。
# 输出：probe/rtti-disasm.txt
import pefile
import struct
import capstone

HOST = r"C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe"
pe = pefile.PE(HOST)
base = pe.OPTIONAL_HEADER.ImageBase
data = pe.get_memory_mapped_image()

md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True

# ---- IAT 映射：VA -> "dll!func" ----
iat = {}
for entry in pe.DIRECTORY_ENTRY_IMPORT:
    dll = entry.dll.decode()
    for imp in entry.imports:
        if imp.name and imp.address:
            iat[imp.address] = dll + "!" + imp.name.decode()

def try_import_at(va):
    """若 va 指向 IAT 槽，返回函数名"""
    if va in iat:
        return iat[va]
    return None

# ---- 解析 BaseClassDescriptor（成员函数表）----
def parse_class_descriptor(cd_rva):
    off = cd_rva
    td_rva, ncb = struct.unpack("<II", data[off:off + 8])
    pmd = struct.unpack("<III", data[off + 8:off + 20])
    attrs, num_members = struct.unpack("<II", data[off + 20:off + 28])
    members = []
    for i in range(num_members):
        m_rva = struct.unpack("<I", data[off + 28 + i * 4:off + 28 + i * 4 + 4])[0]
        members.append(m_rva)
    return td_rva, ncb, pmd, attrs, num_members, members

def disasm_func(va, max_bytes=0x400):
    """反汇编函数体，返回 (calls, interesting) —— calls: IAT 调用清单"""
    code = data[va - base:va - base + max_bytes]
    calls = []
    lines = []
    for ins in md.disasm(code, va):
        if ins.mnemonic in ("ret", "int3") and len(lines) > 8:
            lines.append("  ret")
            break
        if ins.mnemonic in ("call", "jmp"):
            op = ins.op_str
            if "rip" in op:
                # call qword ptr [rip+X] -> 目标 VA = 下条指令地址 + X
                try:
                    disp = int(op.split("+")[-1].rstrip("]").strip(), 16)
                except ValueError:
                    disp = -int(op.split("-")[-1].rstrip("]").strip(), 16)
                target = ins.address + ins.size + disp
                name = try_import_at(target)
                if name:
                    calls.append((ins.mnemonic, name, target))
                    lines.append("  0x%X  %s %s   -> %s" % (ins.address, ins.mnemonic, ins.op_str, name))
                    continue
                # 相对 call 到其它函数
                if op.startswith("0x"):
                    tgt = int(op, 16)
                    calls.append((ins.mnemonic, "sub_" + ("%X" % tgt), tgt))
                    lines.append("  0x%X  %s %s" % (ins.address, ins.mnemonic, ins.op_str))
                    continue
            lines.append("  0x%X  %s %s" % (ins.address, ins.mnemonic, ins.op_str))
    return calls, lines

out = []

# EnableTouchpad@inventec_sync_operation 的 COL @ 0xE0138（上一轮定位），pCD = 0xE0160
targets = [
    ("EnableTouchpad@inventec_sync_operation", 0xE0160),
]
# DisableTouchpad 的 COL：pTD = EnableTouchpad TD + 0x50（相邻布局）→ 搜 DisableTouchpad TD 的引用
name = b".?AVDisableTouchpad@inventec_sync_operation@wmi@base@@"
idx = data.find(name)
td_dis = base + idx - 16
pat = struct.pack("<I", td_dis - base)
for m in __import__("re").finditer(__import__("re").escape(pat), data):
    col_off = m.start() - 12
    if col_off < 0:
        continue
    self_rva = struct.unpack("<I", data[col_off + 20:col_off + 24])[0]
    if self_rva != col_off:
        continue
    pcd = struct.unpack("<I", data[col_off + 16:col_off + 20])[0]
    targets.append(("DisableTouchpad@inventec_sync_operation", pcd))
    break

for cls_name, cd_rva in targets:
    out.append("=== " + cls_name + "  (pCD RVA 0x%X) ===" % cd_rva)
    try:
        td_rva, ncb, pmd, attrs, num_members, members = parse_class_descriptor(cd_rva)
        out.append("  TD RVA=0x%X  含基类=%d  attrs=0x%X  成员函数=%d" % (td_rva, ncb, attrs, num_members))
        out.append("  成员函数 RVA: " + ", ".join("0x%X" % m for m in members))
        for m in members:
            va = base + m
            out.append("  --- 成员函数 0x%X ---" % va)
            calls, lines = disasm_func(va)
            out.extend(lines)
            if calls:
                out.append("  【该函数的调用清单】")
                for mn, name, tgt in calls:
                    out.append("    %s -> %s (0x%X)" % (mn, name, tgt))
    except Exception as ex:
        out.append("  解析失败: " + repr(ex))
    out.append("")

with open(r"D:\AutoClaw\笔记本控制台\zcode\probe\rtti-disasm.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("saved probe/rtti-disasm.txt,", len(out), "行")
