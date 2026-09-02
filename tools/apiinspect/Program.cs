#pragma warning disable CS0618
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

string dll = @"D:/steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data/Managed/Assembly-CSharp.dll";
string[] typeNames = args.Length > 0 ? args : new[] { "OverlayMenu", "OverlayScreen", "OverlayModes", "SimDebugView", "SimDebugView+OverlayModes", "Strings" };
if (typeNames.Length > 0 && File.Exists(typeNames[0]))
{
    dll = typeNames[0];
    typeNames = typeNames.Skip(1).ToArray();
    if (typeNames.Length == 0) typeNames = new[] { "PeterHan.PLib.UI.POverlays", "PeterHan.PLib.UI.POverlayColourProvider", "PeterHan.PLib.UI.POverlayType" };
}

using var fs = File.OpenRead(dll);
using var pe = new PEReader(fs);
var mr = pe.GetMetadataReader();

if (typeNames.Length >= 2 && typeNames[0] == "il")
{
    DumpIL(pe, mr, typeNames[1], typeNames.Length > 2 ? typeNames[2] : null);
    Console.WriteLine("===== DONE =====");
    return;
}

if (typeNames.Length >= 2 && typeNames[0] == "search")
{
    foreach (var needle in typeNames.Skip(1))
    {
        Console.WriteLine($"===== SEARCH '{needle}' =====");
        foreach (var th in mr.TypeDefinitions)
        {
            var td = mr.GetTypeDefinition(th);
            string ns = mr.GetString(td.Namespace);
            var fullPath = new List<string>();
            var cur = th;
            while (true)
            {
                var ctd = mr.GetTypeDefinition(cur);
                fullPath.Insert(0, mr.GetString(ctd.Name));
                var declaring = ctd.GetDeclaringType();
                if (declaring.IsNil) break;
                cur = declaring;
            }
            string path = string.Join("+", fullPath);
            string full = (ns.Length > 0 ? ns + "." : "") + path;
            if (full.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                Console.WriteLine("  " + full);
        }
    }
    Console.WriteLine("===== DONE =====");
    return;
}

foreach (var input in typeNames)
{
    bool found = false;
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        string ns = mr.GetString(td.Namespace);
        var fullPath = new List<string>();
        var cur = th;
        while (true)
        {
            var ctd = mr.GetTypeDefinition(cur);
            fullPath.Insert(0, mr.GetString(ctd.Name));
            var declaring = ctd.GetDeclaringType();
            if (declaring.IsNil) break;
            cur = declaring;
        }
        string path = string.Join("+", fullPath);
        string full = (ns.Length > 0 ? ns + "." : "") + path;
        if (full != input) continue;

        found = true;
        Console.WriteLine($"===== {full} =====");
        // 所有非属性访问器方法（含私有）
        foreach (var mh in td.GetMethods())
        {
            var md = mr.GetMethodDefinition(mh);
            bool isStatic = (md.Attributes & MethodAttributes.Static) != 0;
            string mname = mr.GetString(md.Name);
            if (mname.StartsWith("get_") || mname.StartsWith("set_")) continue;
            var access = (md.Attributes & MethodAttributes.MemberAccessMask) switch
            {
                MethodAttributes.Public => "pub",
                MethodAttributes.Family => "prot",
                MethodAttributes.Assembly => "int",
                MethodAttributes.Private => "priv",
                _ => "?"
            };
            try
            {
                var sig = md.DecodeSignature(new SigProvider(), null);
                var paramNames = md.GetParameters().Select(p => mr.GetString(mr.GetParameter(p).Name)).ToArray();
                var parts = sig.ParameterTypes.ToArray();
                for (int i = 0; i < parts.Length && i < paramNames.Length; i++)
                    parts[i] = paramNames[i] != "" ? $"{parts[i]} {paramNames[i]}" : parts[i];
                Console.WriteLine($"  M [{(isStatic ? "static " : "")}{access}] {sig.ReturnType} {mname}({string.Join(", ", parts)})");
            }
            catch { Console.WriteLine($"  M [{(isStatic ? "static " : "")}{access}] ? {mname}"); }
        }
        foreach (var fh in td.GetFields())
        {
            var fd = mr.GetFieldDefinition(fh);
            bool isStatic = (fd.Attributes & FieldAttributes.Static) != 0;
            string fname = mr.GetString(fd.Name);
            try { Console.WriteLine($"  F [{(isStatic ? "static " : "")}{fd.DecodeSignature(new SigProvider(), null)}] {fname}"); }
            catch { Console.WriteLine($"  F [?]{fname}"); }
        }
        Console.WriteLine("");
        break;
    }
    if (!found) Console.WriteLine($"===== {input} NOT FOUND =====");
}
Console.WriteLine("===== DONE =====");

void DumpIL(PEReader pe, MetadataReader mr, string typeName, string methodName)
{
    foreach (var th in mr.TypeDefinitions)
    {
        var td = mr.GetTypeDefinition(th);
        string ns = mr.GetString(td.Namespace);
        var fullPath = new List<string>();
        var cur = th;
        while (true)
        {
            var ctd = mr.GetTypeDefinition(cur);
            fullPath.Insert(0, mr.GetString(ctd.Name));
            var declaring = ctd.GetDeclaringType();
            if (declaring.IsNil) break;
            cur = declaring;
        }
        string path = string.Join("+", fullPath);
        string full = (ns.Length > 0 ? ns + "." : "") + path;
        if (full != typeName) continue;

        foreach (var mh in td.GetMethods())
        {
            var md = mr.GetMethodDefinition(mh);
            string mname = mr.GetString(md.Name);
            if (methodName != null && mname != methodName) continue;
            Console.WriteLine($"===== IL {full}.{mname} =====");
            if (!md.RelativeVirtualAddress.Equals(0))
            {
                var body = pe.GetMethodBody(md.RelativeVirtualAddress);
                var il = body.GetILBytes();
                int i = 0;
                while (i < il.Length)
                {
                    byte op = il[i++];
                    int start = i - 1;
                    if (op == 0xFE) { byte op2 = il[i++]; if (op2 == 0x06 || op2 == 0x07) { int tok = BitConverter.ToInt32(il, i); i += 4; Console.WriteLine($"  IL_{start:X4}: ldftn {Resolve(mr, tok)}"); } continue; }
                    if (op == 0x72) { int tok = BitConverter.ToInt32(il, i); i += 4; Console.WriteLine($"  IL_{start:X4}: ldstr \"{Resolve(mr, tok)}\""); continue; }
                    if (op == 0x73) { int tok = BitConverter.ToInt32(il, i); i += 4; Console.WriteLine($"  IL_{start:X4}: newobj {Resolve(mr, tok)}"); continue; }
                    if (op == 0x28 || op == 0x6F) { int tok = BitConverter.ToInt32(il, i); i += 4; Console.WriteLine($"  IL_{start:X4}: {(op == 0x28 ? "call" : "callvirt")} {Resolve(mr, tok)}"); continue; }
                    if (op == 0x7B || op == 0x7C || op == 0x80 || op == 0x7D || op == 0x7E) { int tok = BitConverter.ToInt32(il, i); i += 4; Console.WriteLine($"  IL_{start:X4}: {(op == 0x7B ? "ldfld" : op == 0x7C ? "ldflda" : op == 0x7D ? "stfld" : op == 0x7E ? "stfld" : "ldsfld")} {Resolve(mr, tok)}"); continue; }
                    if (op >= 0x14 && op <= 0x1F) { Console.WriteLine($"  IL_{start:X4}: ldc.i4 {op - 0x14}"); continue; }
                    if (op >= 0x20 && op <= 0x26) { Console.WriteLine($"  IL_{start:X4}: ldc.i4.s/ldc.i8 ..."); continue; }
                    if (op == 0x8D) { int tok = BitConverter.ToInt32(il, i); i += 4; Console.WriteLine($"  IL_{start:X4}: newarr {Resolve(mr, tok)}"); continue; }
                    switch (op)
                    {
                        case 0x2A: Console.WriteLine("  IL_0000: ret"); break;
                        case 0x16: case 0x17: case 0x18: case 0x19: case 0x1A: case 0x1B: case 0x1C: case 0x1D: case 0x1E: Console.WriteLine($"  IL_{start:X4}: ldc.i4 {op - 0x16}"); break;
                        case 0x25: case 0x26: Console.WriteLine($"  IL_{start:X4}: dup"); break;
                        case 0x12: Console.WriteLine($"  IL_{start:X4}: ldnull"); break;
                        case 0x00: Console.WriteLine($"  IL_{start:X4}: nop"); break;
                    }
                }
            }
            else Console.WriteLine("  (no body)");
            Console.WriteLine("");
            if (methodName != null) break;
        }
        return;
    }
}

string Resolve(MetadataReader mr, int token)
{
    try
    {
        int table = (int)((uint)token & 0xFF000000);
        int rid = token & 0x00FFFFFF;
        switch (table)
        {
            case 0x70000000: return "\"" + mr.GetUserString(MetadataTokens.UserStringHandle(rid)).Replace("\n", "\\n").Replace("\0", "") + "\"";
            case 0x0A000000:
                var mref = mr.GetMemberReference(MetadataTokens.MemberReferenceHandle(rid));
                string par = mref.Parent.Kind switch
                {
                    HandleKind.TypeReference => GetTRName(mr, (TypeReferenceHandle)mref.Parent),
                    HandleKind.TypeDefinition => GetTDName(mr, (TypeDefinitionHandle)mref.Parent),
                    _ => "typespec"
                };
                return $"{par}::{mr.GetString(mref.Name)}";
            case 0x06000000: { var m = mr.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(rid)); return $"{GetTDName(mr, m.GetDeclaringType())}::{mr.GetString(m.Name)}"; }
            case 0x04000000: { var f = mr.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(rid)); return $"{GetTDName(mr, f.GetDeclaringType())}::{mr.GetString(f.Name)}"; }
            case 0x01000000: { var t = mr.GetTypeReference(MetadataTokens.TypeReferenceHandle(rid)); return $"{GetTRName(mr, MetadataTokens.TypeReferenceHandle(rid))}"; }
            case 0x02000000: { var t = mr.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(rid)); return $"{GetTDName(mr, MetadataTokens.TypeDefinitionHandle(rid))}"; }
            case 0x1B000000: return "typespec";
            default: return $"tok{token:X8}";
        }
    }
    catch { return $"tok{token:X8}"; }
}

string GetTDName(MetadataReader mr, TypeDefinitionHandle h)
{
    var t = mr.GetTypeDefinition(h);
    string ns = mr.GetString(t.Namespace);
    string nm = mr.GetString(t.Name);
    return (ns.Length > 0 ? ns + "." : "") + nm;
}

string GetTRName(MetadataReader mr, TypeReferenceHandle h)
{
    var t = mr.GetTypeReference(h);
    string ns = mr.GetString(t.Namespace);
    string nm = mr.GetString(t.Name);
    return (ns.Length > 0 ? ns + "." : "") + nm;
}

class SigProvider : ISignatureTypeProvider<string, object>
{
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "func*";
    public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;
    public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) { var t = reader.GetTypeDefinition(handle); string ns = reader.GetString(t.Namespace); string nm = reader.GetString(t.Name); return (ns.Length > 0 ? ns + "." : "") + nm; }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) { var t = reader.GetTypeReference(handle); string ns = reader.GetString(t.Namespace); string nm = reader.GetString(t.Name); return (ns.Length > 0 ? ns + "." : "") + nm; }
    public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var ts = reader.GetTypeSpecification(handle);
        return ts.DecodeSignature(this, genericContext);
    }
}