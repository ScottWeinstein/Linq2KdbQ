$ScriptRoot = (split-Path $MyInvocation.Line -parent)
$T4RegPath = "HKLM:\SOFTWARE\Microsoft\VisualStudio\9.0\TextTemplating\IncludeFolders\.tt"
if ((Get-ItemProperty -path $T4RegPath).IncludeLinq2Kdb)
{
    Remove-ItemProperty -path $T4RegPath -Name IncludeLinq2Kdb
}
New-ItemProperty -path $T4RegPath -Name IncludeLinq2Kdb -Value $ScriptRoot\linq2kdb+\CodeGen\
