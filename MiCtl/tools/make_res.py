# 生成 Win32 资源文件（.res）供 csc /win32res 使用：
#   RT_ICON × N + RT_GROUP_ICON("APP")   —— 应用图标（来自 app.ico）
#   RT_VERSION(1)                          —— 版本信息（公司/产品/描述/版本号）
#   RT_MANIFEST(1)                         —— 程序 manifest（/win32res 与 /win32manifest 互斥，必须一起放进来）
# 全部在构建期完成，编译后不再修改 exe —— 不触发安全软件的"可执行文件被改写"行为规则。
# 用法: python make_res.py <输出.res> <版本号> <图标.ico> <manifest>
import struct
import sys


def wstr(s):
    return s.encode("utf-16-le") + b"\x00\x00"


def align(b):
    while len(b) % 4:
        b += b"\x00"
    return b


def res_record(rtype, rname, data, lang=0x0409, flags=0x1030):
    """一条 RES 记录。
    ordinal（数字）：0xFFFF 标记 + WORD 数字（微软 RESOURCEHEADER 的 Name/Ordinal 约定）；
    字符串：裸 UTF-16 字符串（首字符即首 WORD，不能带任何前缀）。
    """

    def field(v):
        if isinstance(v, int):
            return struct.pack("<HH", 0xFFFF, v)
        s = wstr(v)
        pad = (-len(s)) % 4
        return s + b"\x00" * pad

    t, n = field(rtype), field(rname)
    rest = t + n + struct.pack("<IHHII", 0, flags, lang, 0, 0)
    header = struct.pack("<II", len(data), 8 + len(rest)) + rest
    return header + align(data)


def res_blank():
    """rc.exe 的开场记录：type=0、name=0、无数据。csc 用来识别有效的 .res 文件。"""
    return (struct.pack("<II", 0, 32)
            + struct.pack("<HH", 0xFFFF, 0)
            + struct.pack("<HH", 0xFFFF, 0)
            + struct.pack("<IHHII", 0, 0, 0, 0, 0))


def load_ico(path):
    data = open(path, "rb").read()
    reserved, ico_type, count = struct.unpack("<HHH", data[0:6])
    assert ico_type == 1, "不是 icon 文件"
    entries = []
    off = 6
    for _ in range(count):
        w, h, colors, r, planes, bits, size, img_off = struct.unpack("<BBBBHHII", data[off:off + 16])
        entries.append(dict(w=w, h=h, colors=colors, planes=planes, bits=bits, size=size))
        off += 16
    images = [data[struct.unpack("<I", data[6 + i * 16 + 12:6 + i * 16 + 16])[0]:
              struct.unpack("<I", data[6 + i * 16 + 12:6 + i * 16 + 16])[0] + entries[i]["size"]]
              for i in range(count)]
    return entries, images


def group_icon_dir(entries):
    b = struct.pack("<HHH", 0, 1, len(entries))
    for i, e in enumerate(entries):
        # GRPICONDIRENTRY = ICONDIRENTRY 但末尾的 4 字节偏移换成 2 字节资源 ID
        b += struct.pack("<BBBBHHIH", e["w"], e["h"], e["colors"], 0,
                         e["planes"], e["bits"], e["size"], i + 1)
    return b


def string_struct(key, value):
    b = bytearray()
    b += b"\x00\x00"                      # wLength 回填
    b += struct.pack("<H", len(value))    # wValueLength：字符数（不含结尾 null）
    b += struct.pack("<H", 1)             # wType = 文本
    b += wstr(key)
    align(b)
    b += wstr(value)
    align(b)
    b[0:2] = struct.pack("<H", len(b))
    return bytes(b)


def named_block(key, child, value_length=0, wtype=1):
    b = bytearray()
    b += b"\x00\x00"
    b += struct.pack("<H", value_length)
    b += struct.pack("<H", wtype)
    b += wstr(key)
    align(b)
    if child:
        b += child
    b[0:2] = struct.pack("<H", len(b))
    return bytes(b)


def version_info(version, product, description):
    nums = [int(x) for x in version.split(".")] + [0, 0, 0, 0]
    maj, minor, build, rev = nums[:4]
    fixed = struct.pack("<13I",
                        0xFEEF04BD, 0x00010000,
                        (maj << 16) | (minor & 0xFFFF), (build << 16) | (rev & 0xFFFF),
                        (maj << 16) | (minor & 0xFFFF), (build << 16) | (rev & 0xFFFF),
                        0x3F, 0, 0x00040004, 1, 0, 0, 0)
    strings = [
        string_struct("CompanyName", "Lkxuii"),
        string_struct("FileDescription", description),
        string_struct("FileVersion", version),
        string_struct("InternalName", product),
        string_struct("OriginalFilename", product + ".exe"),
        string_struct("ProductName", product),
        string_struct("ProductVersion", version),
        string_struct("LegalCopyright", "Lkxuii"),
    ]
    table = bytearray()
    table += b"\x00\x00" + b"\x00\x00" + struct.pack("<H", 1) + wstr("040904b0")
    for s in strings:
        table += s
    table[0:2] = struct.pack("<H", len(table))
    sfi = named_block("StringFileInfo", bytes(table))

    var = bytearray()
    var += b"\x00\x00" + struct.pack("<H", 4) + b"\x00\x00" + wstr("Translation")
    align(var)
    var += struct.pack("<II", 0x0409, 0x04B0)
    var[0:2] = struct.pack("<H", len(var))
    vfi = named_block("VarFileInfo", bytes(var))

    top = bytearray()
    top += b"\x00\x00" + struct.pack("<H", len(fixed)) + b"\x00\x00" + wstr("VS_VERSION_INFO")
    align(top)
    top += fixed
    top += sfi
    top += vfi
    top[0:2] = struct.pack("<H", len(top))
    return bytes(top)


def main():
    if len(sys.argv) < 5:
        print("用法: make_res.py <输出.res> <版本号> <图标.ico> <manifest>")
        return 2
    out_path, version, ico_path, manifest_path = sys.argv[1:5]
    product = "MiCtl"
    description = "MiCtl - Redmi G 2021 笔记本硬件控制台"

    entries, images = load_ico(ico_path)
    manifest = open(manifest_path, "rb").read()
    ver = version_info(version, product, description)

    out = bytearray()
    out += res_blank()
    for i, img in enumerate(images):
        out += res_record(1, i + 1, img)                    # RT_ICON
    out += res_record(14, 1, group_icon_dir(entries))       # RT_GROUP_ICON
    out += res_record(16, 1, ver)                           # RT_VERSION
    out += res_record(24, 1, manifest, lang=0x0409)          # RT_MANIFEST

    open(out_path, "wb").write(out)
    print("saved", out_path, len(out), "bytes,", len(images), "icons, manifest", len(manifest), "bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
