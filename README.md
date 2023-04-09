<div align="center">
  <h1><code>Wasm.SourceGen</code></h1>

  <p>
    <strong>
    C# Source generator for developing WASI modules and components
    </strong>
  </p>
</div>

## About
> **Note**: this project is experimental and should not be used in any serious context

[dotnet-wasi-sdk]: https://github.com/SteveSandersonMS/dotnet-wasi-sdk
[Component Model]: https://github.com/WebAssembly/component-model/blob/main/design/mvp/WIT.md

This repository contains a C# source generator for generating WASI modules from pure C# code. It does this by generating C code calling into the mono C driver. This can can then be compiled to a WASI module using the [dotnet-wasi-sdk]. In the future, this will hopefully support the [Component Model].

The package has 2 projects:
- [Wasm.SourceGen](Wasm.SourceGen) is a class library holding types and functions relating to the source generator, this namespace can be used by guest modules and C# hosts.
- [Wasm.SourceGen.Analyzers](Wasi.SourceGen.Analyzers) contains the actual C# source generator.

## Usage

### Parameter & Return types
Not all types are currently supported, or will be supported. My goal is to get to the following supported at least:
- built-in types (strings, int, float, etc)
- Arrays
- Arbitrary classes and structs 

For now only `string` works as function parameter, with `int` or `void` as return :)

### Exporting a function
A function can be exposed by applying the `WasmExportAttribute` from `Wasm.SourceGen` to a static method:

```csharp
[WasmExport("hello")]
public static void Hello(string name)
{
  Console.WriteLine($"Hello {name}, from WASI");
}
```

The above will export the `Hello` function and expose it as `hello` in the WASM module. It can be called with wasmtime like so:
```csharp
//...
var hello = instance.GetFunction<int, int>("hello");
var name = WasmtimeUtils.CreateWasmString(instance, "MyName");
hello.Invoke(name);

```

### Importing a function
A function can be imported from a WASM host by applying the `Wasm.SourceGen.WasmImportAttribute` to a `public static extern` method marked with `[MethodImpl(MethodImplOptions.InternalCall)]`:
```csharp 
[MethodImpl(MethodImplOptions.InternalCall)]
[WasmImport("env", "hello")]
public static extern void Hello();
```

### Executing

The project can be built and run by executing:
> make run

## Contributing
If you are interested in contributing, please open an issue to discuss the desired change. Proactive PR's are also very much welcome!