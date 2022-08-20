dotnet new sln --force -n duplicate-file-finder
dotnet sln duplicate-file-finder.sln add (ls -r ./src/**/*.csproj)
