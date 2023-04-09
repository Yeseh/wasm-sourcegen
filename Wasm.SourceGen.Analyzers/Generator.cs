using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Wasm.SourceGen.Analyzers
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        const string WasiExportAttributeName = "WasmExportAttribute";

        const string WasiImportAttributeName = "WasmImportAttribute";

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


        class WasiMethodSyntaxReceiver : ISyntaxContextReceiver
        {
            public MethodType MethodType { get; private set; }
            public List<WasmMethod> WasiMethods { get; private set; } = new List<WasmMethod>(); 

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

                    var wasmMethod = new WasmMethod()
                    {
                        Assembly = symbol.ContainingAssembly.Name,
                        Namespace = symbol.ContainingNamespace.Name,
                        Class = symbol.ContainingType.Name,
                        Name = symbol.Name,
                        ReturnType = symbol.ReturnType,
                        IsStatic = symbol.IsStatic
                    };

                    // TODO: Add 'this' parameter to params somehow
                    if (wasmMethod.IsStatic) {}

                    // TODO: Feedback for attributes
                    if (exportAttribute != null) 
                    { 
                        wasmMethod.Type = MethodType.Export;
                        wasmMethod.WasmFunctionName = exportAttribute.ConstructorArguments.FirstOrDefault().Value.ToString();
                    }
                    else if (importAttribute != null) 
                    { 
                        wasmMethod.Type = MethodType.Import; 
                        wasmMethod.WasmModule = importAttribute.ConstructorArguments[0].Value.ToString();
                        wasmMethod.WasmFunctionName = importAttribute.ConstructorArguments[1].Value.ToString();
                    }

                    foreach (var param in methodDeclaration.ParameterList.Parameters)
                    {
                        var ident = param.Identifier.ValueText;
                        var parSymbol = (IParameterSymbol)context.SemanticModel.GetDeclaredSymbol(param);
                        wasmMethod.Params.Add(new WasmMethodParameter
                        {
                            Ident = param.Identifier.ValueText,
                            TypeSymbol = parSymbol.Type
                        });
                    }

                    WasiMethods.Add(wasmMethod);
                }
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var methods = ((WasiMethodSyntaxReceiver)context.SyntaxContextReceiver).WasiMethods;
            var imports = methods.Where(m => m.Type == MethodType.Import);
            var exports = methods.Where(m => m.Type == MethodType.Export);

            var internalCalls = new List<string>(imports.Count());
            var importDecls = new List<string>(imports.Count());
            var exportDecls = new List<string>(exports.Count());
            var exportPointers = new List<string>(exports.Count());

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

            if (methods.Count > 0)
            {
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
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                File.WriteAllText(Path.Combine(outputDir, "interop.gen.c"), source); 
            }
        }

        private string ExportPointer(GeneratorExecutionContext context, WasmMethod method)
            => $"MonoMethod* method_{method.Name};";

        private string ExportFunctionDeclaration(GeneratorExecutionContext context, WasmMethod method)
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
MonoObject* __wasm_export_{method.Name.ToLowerSnakeCase()}({thisParam}{paramListJoin}) {{
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

        private string ImportDeclaration(GeneratorExecutionContext context, WasmMethod method)
            => $"__attribute__((__import_module__(\"{method.WasmModule}\"), __import_name__(\"{method.WasmFunctionName}\")))\n"
                + $"extern void __wasm_import_{method.WasmModule.ToLowerSnakeCase()}_{method.Name.ToLowerSnakeCase()}();";

        private string InternalCall(GeneratorExecutionContext context, WasmMethod import)
            => $"mono_add_internal_call(\"{import.Namespace}.{import.Class}::{import.Name}\", __wasm_import_{import.WasmModule.ToLowerSnakeCase()}_{import.Name.ToLowerSnakeCase()});";
        
        public void Initialize(GeneratorInitializationContext context) 
        {
            context.RegisterForSyntaxNotifications(() => new WasiMethodSyntaxReceiver());
        }
    }
}
