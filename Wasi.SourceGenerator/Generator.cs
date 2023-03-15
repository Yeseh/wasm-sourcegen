using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

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

        public List<WasiMethodParameter> Params { get; set; } = new List<WasiMethodParameter>();

        public string FullyQualifiedMethodName => $"{Namespace}.{Class}::{Name}";
    }

    class WasiMethodParameter
    {
        public string Identitfier { get; set; }

        public string Type { get; set; }

        public string CType()
        {
            var crepr = string.Empty;
            switch (Type)
            {
                default:
                    throw new Exception("Unsupported type");
            }

            return crepr;
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
                    var canExport = symbol.IsStatic;
                    var canImport = symbol.IsStatic && symbol.IsExtern;

                    var wasiMethod = new WasiMethod()
                    {
                        Assembly = symbol.ContainingAssembly.Name,
                        Namespace = symbol.ContainingNamespace.Name,
                        Class = symbol.ContainingType.Name,
                        Name = symbol.Name
                    };

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

                    foreach (var param in methodDeclaration.ParameterList.Parameters)
                    {
                        var ident = param.Identifier.ValueText;
                        wasiMethod.Params.Add(new WasiMethodParameter
                        {
                            Identitfier = param.Identifier.ValueText,
                            Type = ((IParameterSymbol)param.Type).Type.ToString(),
                        });
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
                        exportDecls.Add(ExportDeclaration(context, method));
                        exportPointers.Add(ExportPointer(context, method));
                        break;
                } 
            }

            var source = $@"// <auto-generated>

#include <mono-wasi/driver.h>
#include <assert.h>
#include <string.h>

{string.Join("\n", exportPointers)}

{string.Join("\n", importDecls)}

{string.Join("\n", exportDecls)}

void attach_internal_calls() {{
    {string.Join("\n\t", internalCalls)}
}}

";

            var outputDir = Path.Combine(Path.GetDirectoryName(context.Compilation.SyntaxTrees.First().FilePath), "native");
            var filePath = Path.Combine(outputDir, "interop.gen.c");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Debug.WriteLine($"Created directory: {outputDir}");
            }
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.WriteAllText(Path.Combine(outputDir, "interop.gen.c"), source); 
        }

        private string ExportPointer(GeneratorExecutionContext context, WasiMethod method)
            => $"MonoMethod* method_{method.Name};";

        private string ExportDeclaration(GeneratorExecutionContext context, WasiMethod method)
        {
            var source = $@"
__attribute__((export_name(""{method.WasmFunctionName}"")))
void wasm_export_{method.Name.ToLowerSnakeCase()}() {{
    if(!method_{method.Name}) {{
        method_{method.Name} = lookup_dotnet_method(""{method.Assembly}.dll"", ""{method.Namespace}"", ""{method.Class}"", ""{method.Name}"", -1);
        assert(method_{method.Name});
    }}
    MonoObject* exception;
    void* method_params[] = {{}};
    mono_wasm_invoke_method(method_{method.Name}, NULL, method_params, &exception);
    assert(!exception);
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
