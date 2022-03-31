# RepoReferences
SVN Repository nuget analysis. Fetch Dictionary&lt;Reference, Projects> from SVN repository
## Use case
You need to fetch list of nuget|dll|package references of all *.csproj* files from huge SVN repository
## How to use
###
Checkout code. Change code for your own needs. Build.
###
Get list of your .csproj's for better console app performance. 
Do it like: 
```ps
 cmd> svn list -R  https://svn.your.domain.com/your/repo | findstr /e ".csproj" > "C:\any\local\path.txt"
```
###
Call console executable. You may want to redirect console output to file using cmd syntax `> "C:\another\local\path.txt"`. 
Do it like: 
```ps
cmd> "D:\...\RepoReferences\bin\Release\net6.0\publish\RepoReferences.exe" svnuser@name svnpassword https://svn.your.domain.com/your/repo C:\any\local\path.txt
```
Console output ReferenceName-ProjectNameList dictionary. Copy console output.
###
You may want to paste to excel app output using advanced master copy-paste option to get result as follows (in order to treat \t symbol from app output as column separator):
![App output at excel][app-output-at-excel]

[app-output-at-excel]: images/app-output-at-excel.png
