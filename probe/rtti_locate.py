# 定位 GamingBoxHost.exe（x64）中 inventec_sync_operation 的 Enable/DisableTouchpad/FnKey 类
# 的 TypeDescriptor → CompleteObjectLocator → vftable → 虚函数地址（MSVC x64 RTTI 链，引用用 RVA）
# 输出：probe/rtti-vftable.txt
import pefile
import struct
import re

HOST = r"C:\Program Files (x86)\Timi Personal Computing\GamingBox\1.4.0.851\GamingBoxHost.exe"
pe = pefile.PE(HOST)
base = pe.OPTIONAL_HEADER.ImageBase
data = pe.get_memory_mapped_image()

print("ImageBase=0x%X  Machine=%s" % (base, hex(pe.FILE_HEADER.Machine)))

name_marker = b".?AV"
interesting_names = []
for m in re.finditer(re.escape(name_marker), data):
    name_off = m.start()
    end = data.find(b"\x00", name_off)
    name = data[name_off:end].decode("ascii", errors="replace")
    if "EnableTouchpad" in name or "DisableTouchpad" in name or "EnableFnKey" in name or "TouchpadStatus" in name or "FnKeyStatus" in name:
        td_va = base + name_off - 16   # x64 TD：{vptr(8), spare(8), name[]}
        interesting_names.append((name, td_va))

print("TypeDescriptor 命中:")
for n, va in interesting_names:
    print("  0x%08X  %s" % (va, n))

results = []
for n, td_va in interesting_names:
    td_rva = td_va - base
    pat = struct.pack("<I", td_rva)          # x64 COL 的 pTypeDescriptor 是 RVA(4)
    for m in re.finditer(re.escape(pat), data):
        col_off = m.start() - 16             # x64 COL：{sig(4), offset(4), cdOffset(4), pTD(4), pCD(4)} = 20 字节，pTD 在 COL+12
        if col_off < 0:
            continue
        sig, off, cdoff = struct.unpack("<III", data[col_off:col_off + 12])
        # 用 selfRva（COL+20，指向 COL 自身）验证：比固定 signature 更可靠（实测 sig=1）
        self_rva = struct.unpack("<I", data[col_off + 20:col_off + 24])[0]
        if self_rva != col_off:
            continue
        col_rva = col_off
        colpat = struct.pack("<I", col_rva)  # vftable[-1] 存 COL 的 RVA(4)
        for m2 in re.finditer(re.escape(colpat), data):
            vf_rva = m2.start() + 4
            vf_va = base + vf_rva
            funcs = struct.unpack("<6Q", data[vf_rva:vf_rva + 48])
            results.append((n, td_va, col_rva, vf_va, funcs))

print("")
print("COL / vftable 链:")
for n, td_va, col_rva, vf_va, funcs in results:
    print("  " + n.split("@@")[0] + "@@")
    print("    TD=0x%08X  COL RVA=0x%X  vftable=0x%08X" % (td_va, col_rva, vf_va))
    for i, f in enumerate(funcs):
        if f:
            print("      vft[%d] = 0x%08X" % (i, f))

with open(r"D:\AutoClaw\笔记本控制台\zcode\probe\rtti-vftable.txt", "w", encoding="utf-8") as f:
    f.write("x64 TD → COL → vftable 定位结果\n")
    for n, td_va, col_rva, vf_va, funcs in results:
        f.write("%s\n  TD=0x%08X COL_RVA=0x%X vftable=0x%08X\n" % (n, td_va, col_rva, vf_va))
        for i, fu in enumerate(funcs):
            if fu:
                f.write("    vft[%d] = 0x%08X\n" % (i, fu))
print("saved probe/rtti-vftable.txt")
