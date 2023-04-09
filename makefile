.PHONY: run
run: build
	dotnet run --project SourceGenHost/SourceGenHost.csproj

.PHONY: build
build: genlib gen guest host 

genlib:
	dotnet build ./Wasi.SourceGenerator
gen: 
	dotnet build ./Wasi.SourceGenerator.Analyzers
guest: 
	dotnet build ./SourceGenExample --no-incremental
host: 
	dotnet build ./SourceGenHost