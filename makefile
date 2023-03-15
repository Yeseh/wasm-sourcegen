.PHONY: run
run: build
	dotnet run --project SourceGenHost/SourceGenHost.csproj

.PHONY: build
build: gen guest host

gen: 
	dotnet build ./Wasi.SourceGenerator
guest: 
	dotnet build ./SourceGenExample --no-incremental
host: 
	dotnet build ./SourceGenHost