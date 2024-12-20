# WASAotExample

```powershell
dotnet publish -r win-x64 -o bin\publish-x64 -p:PublishAot=true
.\bin\publish-x64\WASAotExample.exe
```

文件大小主要构成 60MB，主程序 6MB，剩余都是框架。

winuiedit.dll 3mb (一个code editer控件。为什么内置这类我不需要的控件，从哪里引入的)