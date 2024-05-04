dotnet publish -r linux-arm --self-contained
scp -r "F:\VR\H3VR Modding\H3MPIndexServer\H3MPIndexServer\bin\Release\net6.0\linux-arm" vip@192.168.0.165:/home/vip/Software/H3MPIndexServer
Read-Host -Prompt "Press Enter to exit"