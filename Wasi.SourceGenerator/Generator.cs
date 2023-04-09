using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace Wasi.SourceGenerator
{
    class WasiMethod
    {
        public string Assembly { get; set; }
        public string Namespace { get; set; }
        public string Class { get; set; }
        public string Name { get; set; }
        public MethodType Type { get; set; }
        public string WasmModule { get; set; }
        public string WasmFunctionName{ get; set; }
        public string WasmNamespace { get; set; }

        public bool IsStatic { get; set; }

        public List<WasiMethodInputParameter> Params { get; set; } = new List<WasiMethodInputParameter>();

        public ITypeSymbol ReturnType { get; set; }

        public string FullyQualifiedMethodName => $"{Namespace}.{Class}::{Name}";
    }

    class WasiMethodInputParameter
    {
        public string Ident { get; set; }

        public ITypeSymbol TypeSymbol { get; set; }

        public bool IsArray => TypeSymbol.TypeKind == TypeKind.Array;
        public bool IsClass => TypeSymbol.TypeKind == TypeKind.Class;
        public bool IsStruct => TypeSymbol.TypeKind == TypeKind.Struct;
        public bool IsInterface => TypeSymbol.TypeKind == TypeKind.Interface;
        public string TypeIdent => TypeSymbol.Name.ToLowerInvariant();
        public string CIdent => this.Ident.ToLowerSnakeCase();

        public bool NeedsTransform 
            => Generator.CTransformTemplates.ContainsKey(TypeSymbol.Name.ToString().ToLower());

        public bool NeedsCleanup 
            => Generator.CCleanupTemplates.ContainsKey(TypeSymbol.Name.ToString().ToLower());

        public string CTransform
        {
            get
            {
                var typeName = TypeIdent; 
                if (IsArray) { typeName = "array"; }
                return string.Format(Generator.CTransformTemplates[TypeIdent], CIdent);
            }
        }

        public string CParam {
            get
            {
                var typeName = TypeIdent;
                var hasCParam = Generator.CInputParam.TryGetValue(typeName, out var template);
                if (hasCParam) { return string.Format(template, CIdent); }

                if (IsArray) { typeName = "array"; }
                if (IsClass || IsInterface) { typeName = "object"; }
                if (IsStruct) { throw new NotSupportedException("Struct types are not supported as method parameters"); }
                hasCParam = Generator.CInputParam.TryGetValue(typeName, out template);
                if (!hasCParam) { throw new NotSupportedException($"Type {typeName} was is not supported as input parameter"); }

                var formatted = string.Format(template, CIdent);
                return formatted; 
            }
        }

        public string CCleanup
        {
            get
            {
                return string.Format(Generator.CCleanupTemplates[TypeIdent], CIdent);
            }
        }

        private bool IsArrayOrIEnumerable()
        {
            // Check if the type is an array
            if (TypeSymbol.TypeKind == TypeKind.Array)
            {
                return true;
            }

            // Check if the type implements IEnumerable<T>
            INamedTypeSymbol ienumerableSymbol = TypeSymbol.ContainingAssembly
                .GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");

            if (ienumerableSymbol != null && TypeSymbol.AllInterfaces.Contains(ienumerableSymbol))
            {
                return true;
            }

            // Check if the type implements IEnumerable
            INamedTypeSymbol ienumerableNonGenericSymbol = TypeSymbol.ContainingAssembly
                .GetTypeByMetadataName("System.Collections.IEnumerable");

            if (ienumerableNonGenericSymbol != null && TypeSymbol.AllInterfaces.Contains(ienumerableNonGenericSymbol))
            {
                return true;
            }

            // If the type is not an array or does not implement IEnumerable, return false
            return false;
        }

    }

    enum MethodType
    {
        Import,
        Export
    }

    [Generator]
    public class Generator : ISourceGenerator
    {

        internal static Dictionary<string, string> CInputParam = new Dictionary<string, string>()
        {
            { "string", "char* {0}" },
            { "int", "int {0}" },
            { "bool", "bool {0}" },
            { "object", "MonoObject* {0}" },
            { "array", "void* {0}_ptr, int {0}_len" },
        };

        internal static Dictionary<string, string> CCleanupTemplates = new Dictionary<string, string>()
        {
            { "string", "free({0});"}
        };
        
        internal static Dictionary<string, string> CTransformTemplates = new Dictionary<string, string>()
        {
            { "string", "MonoString* {0}_trans = mono_wasm_string_from_js({0});"},
            { "array", "MonoArray* {0}_dotnet_array = {0}_ptr ? mono_wasm_typed_array({0}_ptr, {0}_len) : NULL" }
        };

        const string WasiExportAttributeName = "WasiExportAttribute";

        const string WasiImportAttributeName = "WasiImportAttribute";

        class WasiMethodSyntaxReceiver : ISyntaxContextReceiver
        {
            public MethodType MethodType { get; private set; }
            public List<WasiMethod> WasiMethods { get; private set; } = new List<WasiMethod>(); 

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is MethodDeclarationSyntax methodDeclaration) 
                { 
                    var symbol = (IMethodSymbol)context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                    var attributes = symbol.GetAttributes();
                    if (!attributes.Any()) { return; }

                    var exportAttribute = attributes.FirstOrDefault(a => a.AttributeClass.Name == WasiExportAttributeName);
                    var importAttribute = attributes.FirstOrDefault(a => a.AttributeClass.Name == WasiImportAttributeName);

                    if (exportAttribute == null && importAttribute == null ) { return; }
                    var canImport = symbol.IsStatic && symbol.IsExtern;

                    var wasiMethod = new WasiMethod()
                    {
                        Assembly = symbol.ContainingAssembly.Name,
                        Namespace = symbol.ContainingNamespace.Name,
                        Class = symbol.ContainingType.Name,
                        Name = symbol.Name,
                        ReturnType = symbol.ReturnType,
                        IsStatic = symbol.IsStatic
                    };

                    // TODO: Add 'this' parameter to params somehow
                    if (wasiMethod.IsStatic)
                    {

                    }

                    if (exportAttribute != null) 
                    { 
                        wasiMethod.Type = MethodType.Export;

                        // TODO: Doesn't deal with multiple constructors nicely
                        for (int i = 0, n = exportAttribute.ConstructorArguments.Length; i < n; i++)
                        { 
                            var arg = exportAttribute.ConstructorArguments[i];
                            switch (i)
                            {
                                case 0: 
                                    wasiMethod.WasmNamespace = arg.Value.ToString();
                                    break;
                                case 1: 
                                    wasiMethod.WasmModule= arg.Value.ToString();
                                    break;
                                case 2: 
                                    wasiMethod.WasmFunctionName = arg.Value.ToString();
                                    break;
                            }
                        }

                        foreach (var param in methodDeclaration.ParameterList.Parameters)
                        {
                            var ident = param.Identifier.ValueText;
                            var parSymbol = (IParameterSymbol)context.SemanticModel.GetDeclaredSymbol(param);
                            wasiMethod.Params.Add(new WasiMethodInputParameter
                            {
                                Ident = param.Identifier.ValueText,
                                TypeSymbol = parSymbol.Type
                            });
                        }
                    }
                    else if (importAttribute != null) 
                    { 
                        for (int i = 0, n = importAttribute.ConstructorArguments.Length; i < n; i++)
                        { 
                            var arg = importAttribute.ConstructorArguments[i];
                            switch (i)
                            {
                                case 0: 
                                    wasiMethod.WasmNamespace = arg.Value.ToString();
                                    break;
                                case 1: 
                                    wasiMethod.WasmModule= arg.Value.ToString();
                                    break;
                                case 2: 
                                    wasiMethod.WasmFunctionName = arg.Value.ToString();
                                    break;
                            }
                        }
                    }

                    WasiMethods.Add(wasiMethod);
                }
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var methods = ((WasiMethodSyntaxReceiver)context.SyntaxContextReceiver).WasiMethods;
            var imports = methods.Where(m => m.Type == MethodType.Import);
            var exports = methods.Where(m => m.Type == MethodType.Export);

            var internalCalls = new List<string>();
            var importDecls = new List<string>();
            var exportDecls = new List<string>();
            var exportPointers = new List<string>();

            foreach (var method in methods)
            {
                switch (method.Type) 
                {
                    case MethodType.Import:
                        internalCalls.Add(InternalCall(context, method));
                        importDecls.Add(ImportDeclaration(context, method));
                        break;
                    case MethodType.Export:
                        exportDecls.Add(ExportFunctionDeclaration(context, method));
                        exportPointers.Add(ExportPointer(context, method));
                        break;
                } 
            }

            var source = $@"// <auto-generated>

#include <mono-wasi/driver.h>
#include <assert.h>
#include <string.h>

MonoClass* mono_get_byte_class(void);
MonoDomain* mono_get_root_domain(void);

MonoArray* mono_wasm_typed_array_new(void* arr, int length) {{
    MonoClass* typeClass = mono_get_byte_class();
    MonoArray* buffer = mono_array_new(mono_get_root_domain(), typeClass, length);
    memcpy(mono_array_addr_with_size(buffer, 1, 0), arr, length);
    return buffer;
}}

{string.Join("\n", importDecls)}

{string.Join("\n", exportPointers)}

{string.Join("\n", exportDecls)}

void fake_settimeout(int timeout) {{
    //
}}

void attach_internal_calls() {{
    mono_add_internal_call(""System.Threading.TimerQueue::SetTimeout"", fake_settimeout);
    {string.Join("\n\t", internalCalls)}
}}

";

            var outputDir = Path.Combine(Path.GetDirectoryName(context.Compilation.SyntaxTrees.First().FilePath), "native");
            var filePath = Path.Combine(outputDir, "interop.gen.c");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            File.WriteAllText(Path.Combine(outputDir, "guid.interop.gen.c"), source); 
        }

        private string ExportPointer(GeneratorExecutionContext context, WasiMethod method)
            => $"MonoMethod* method_{method.Name};";

        private string ExportFunctionDeclaration(GeneratorExecutionContext context, WasiMethod method)
        {
            var paramList = new List<string>();
            var transforms = new List<string>();
            var cleanUps = new List<string>();
            var invokeParams = new List<string>();

            foreach (var par in method.Params)
            {
                paramList.Add($"{par.CParam}");
                if (par.NeedsTransform)
                {
                    transforms.Add(par.CTransform);   
                    invokeParams.Add($"{par.CIdent}_trans");
                }
                else { invokeParams.Add(par.CIdent); }
                if (par.NeedsCleanup) { cleanUps.Add(par.CCleanup); }
            }

            var isNonStaticExport = method.IsStatic && method.Type == MethodType.Export;
            var thisParam = !isNonStaticExport ? "MonoObject* dotnet_target_instance," : string.Empty;
            var thisInvoke = !isNonStaticExport ? "dotnet_target_instance" : "NULL";
            var paramListJoin = string.Join(",", paramList);
            var invokeJoin = string.Join(",", invokeParams);
            var cleanUpsJoin = string.Join("\n\t", cleanUps);
            var transformsJoin = string.Join("\n\t", transforms);

            var source = $@"
__attribute__((export_name(""{method.WasmFunctionName}"")))
void wasm_export_{method.Name.ToLowerSnakeCase()}({thisParam}{paramListJoin}) {{
    if(!method_{method.Name}) {{
        method_{method.Name} = lookup_dotnet_method(""{method.Assembly}.dll"", ""{method.Namespace}"", ""{method.Class}"", ""{method.Name}"", -1);
        assert(method_{method.Name});
    }}
    
    {transformsJoin}

    MonoObject* exception;
    void* method_params[] = {{ {invokeJoin} }};
    MonoObject* res = mono_wasm_invoke_method(method_{method.Name}, {thisInvoke}, method_params, &exception);
    assert(!exception);

    {cleanUpsJoin}

    return res;
}}";

            return source;
        }

        private string ImportDeclaration(GeneratorExecutionContext context, WasiMethod method)
            => $"__attribute__((__import_module__(\"{method.WasmModule}\"), __import_name__(\"{method.WasmFunctionName}\")))\n"
                + $"extern void wasm_import_{method.Name.ToLowerSnakeCase()}();";

        private string InternalCall(GeneratorExecutionContext context, WasiMethod import)
            => $"mono_add_internal_call(\"{import.Namespace}.{import.Class}::{import.Name}\", wasm_import_{import.Name.ToLowerSnakeCase()});";
        
        public void Initialize(GeneratorInitializationContext context) 
        {
            context.RegisterForSyntaxNotifications(() => new WasiMethodSyntaxReceiver());
        }
    }
}
