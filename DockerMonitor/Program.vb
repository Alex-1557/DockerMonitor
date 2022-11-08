Imports System
Imports System.ComponentModel
Imports System.Diagnostics.Metrics
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports BackendAPI
Imports BackendAPI.Docker
Imports BackendAPI.Helper
Imports BackendAPI.Model
Imports BackendAPI.Services
Imports BackendAPI.Vm
Imports DockerMonitor.Helper
Imports Microsoft.AspNetCore.Components.RenderTree
Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Net.Http
Imports MySqlConnector

Module Program
    Dim ClearCN As String
    Dim ClearVmPass As String
    Dim DockerHubVm As Integer
    Dim _DB As ApplicationDbContext
    Dim VmI As Integer
    Dim ProjectAes As AesCryptor
    Dim CN As MySqlConnection
    Dim OptionsBuilder As DbContextOptionsBuilder(Of ApplicationDbContext)
    Dim BackendBaseURL As String
    Dim ContainerStart As String
    Dim ContainerStop As String
    Dim LogDockerEvents As String
    Dim LogDockerEventArr As String()
    Dim LogSshError As Boolean
    Dim SendBackendNotification As Boolean

    Sub Main(args As String())

        Dim CurDomain As AppDomain = AppDomain.CurrentDomain
        AddHandler CurDomain.FirstChanceException, AddressOf FirstChanceException
        AddHandler CurDomain.UnhandledException, AddressOf UnhandledException

        Dim Config As IConfiguration = New ConfigurationBuilder().
            AddJsonFile("appsettings.json").
            AddEnvironmentVariables().
            Build()
        Dim CryptoCN = Config.GetConnectionString("DefaultConnection")
        DockerHubVm = Config.GetValue(Of Integer)("DockerHubVm:ID")
        Dim CryptoPass = Config.GetValue(Of String)("DockerHubVm:VmConnectionDecryptPass")
        LogDockerEvents = Config.GetValue(Of String)("Output:LogDockerEvents")
        LogDockerEventArr = LogDockerEvents.Split(",")
        LogSshError = Config.GetValue(Of Boolean)("Output:LogSshError")
        SendBackendNotification = Config.GetValue(Of Boolean)("Output:SendBackendNotification")
        BackendBaseURL = Config.GetValue(Of String)("Backend:BaseURL")
        ContainerStart = Config.GetValue(Of String)("Backend:ContainerStart")
        ContainerStop = Config.GetValue(Of String)("Backend:ContainerStop")
        NotificationTokenLogin = Config.GetValue(Of String)("NotificationToken:Login")
        NotificationTokenPass = Config.GetValue(Of String)("NotificationToken:Password")

        ProjectAes = New AesCryptor

        ClearCN = ProjectAes.DecryptSqlConnection(CryptoCN, "bGVubWF4")
        OptionsBuilder = New DbContextOptionsBuilder(Of ApplicationDbContext)
        OptionsBuilder.UseMySql(ClearCN,
                                ServerVersion.Parse("10.5.9-MariaDB-1:10.5.9+maria~xenial"), 'SHOW VARIABLES LIKE "%version%";
                                Sub(ByVal mySqlOption As Microsoft.EntityFrameworkCore.Infrastructure.MySqlDbContextOptionsBuilder)
                                    mySqlOption.CommandTimeout(10)
                                    mySqlOption.EnableRetryOnFailure(10)
                                End Sub)
        _DB = New ApplicationDbContext(OptionsBuilder.Options)
        CN = _DB.Database.GetDbConnection()
        CN.Open()

        If _DB.Database.GetDbConnection().State = Data.ConnectionState.Open Then
            Console.Write($"Started for DockerHub {DockerHubVm}, Db opened, ")
        End If

        Dim CurDockerHub = _DB.RawSqlQuery(Of Integer)($"SELECT ToVM FROM DockerHubVm Where i={DockerHubVm};", Function(X) X("ToVm"))
        If CurDockerHub.Item2 Is Nothing Then
            VmI = CurDockerHub.Item1(0)
        Else
            Console.WriteLine(CurDockerHub.Item2.ToString)
            Stop
        End If

        _DB = New ApplicationDbContext(OptionsBuilder.Options)
        CN = _DB.Database.GetDbConnection()
        CN.Open()
        Dim CurrentVM = Sql.ExecRDR(Of TmpVmAccess)(_DB, $"SELECT Name, AdminLogin, aes_decrypt(AdminPassword,'{CryptoPass}') as DecryptedPass  FROM `VM` where i={VmI};",
                                                   Function(X)
                                                       Return New TmpVmAccess With {
                                                       .Name = X("Name"),
                                                       .AdminLogin = X("AdminLogin"),
                                                       .AdminPassword = If(IsDBNull(X("DecryptedPass")), "", Text.UTF8Encoding.UTF8.GetString(X("DecryptedPass")))
                                                       }
                                                   End Function)
        If CurrentVM.Count = 0 Then
            Console.WriteLine($"Vm {VmI} absent")
            Stop
        Else
            Console.Write($"VmName {CurrentVM(0).Name}, ")
        End If

Bash:
        _DB = New ApplicationDbContext(OptionsBuilder.Options)
        CN = _DB.Database.GetDbConnection()
        CN.Open()
        Dim Vm As New VmBashAsync2(_DB, ProjectAes, CurrentVM(0).Name, CryptoPass)
        Dim Connect = Vm.SSHServerConnect()
        If Connect.Item1 IsNot Nothing Then
            Console.WriteLine("Vm terminal opened. Waiting docker events....")
            Dim Ret1 = Vm.Bash($"sudo -S <<< ""{CurrentVM(0).AdminPassword.Replace("$", "\$")}"" sudo docker events", New Threading.CancellationTokenSource, AddressOf NewLine, AddressOf ErrLine)
            Ret1.Wait()
            ' this command never ended, only with error in NewLine/SaveLogToDb/Inspect/SendNotification or any another place<<<---
            Console.WriteLine($"{Now} Unexpected stop. Restarted")
            'Task.Delay(10000).Wait()
            GoTo Bash
        Else
            Console.WriteLine($"Not connected. Restart. SSH ={Connect.Item2?.Message}, DB ={Connect.Item3?.Message}")
            Stop
        End If
    End Sub
    Sub UnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Console.WriteLine((CType(e.ExceptionObject, Exception).Message))
    End Sub
    Sub FirstChanceException(sender As Object, e As FirstChanceExceptionEventArgs)
        Console.WriteLine(e.Exception.Message)
    End Sub

    Sub NewLine(sender As Object, e As KeyValuePair(Of Integer, String))
        Debug.WriteLine($"{e.Key}:{e.Value}")
        SaveLogToDb(sender, e, AddressOf Inspect)
    End Sub

    Sub ErrLine(sender As Object, e As KeyValuePair(Of Integer, String))
        Debug.WriteLine($"Error {e.Key}:{e.Value}")
        If LogSshError Then
            SaveLogToDb(sender, e, Nothing)
        End If
    End Sub

    Sub SaveLogToDb(sender As Object, e As KeyValuePair(Of Integer, String), SendNotification As EventHandler(Of String))
        Dim Lines As String() = e.Value.Split(vbLf)
        For i As Integer = 0 To Lines.Count - 1
            If Not String.IsNullOrWhiteSpace(Lines(i)) Then
                If LogDockerEvents = "All" Then
                    GoTo Write
                Else
                    For j As Integer = 0 To LogDockerEventArr.Count - 1
                        If Lines(i).Contains(LogDockerEventArr(j)) Then
                            GoTo Write
                        End If
                    Next
                    Continue For
                End If
Write:
                _DB = New ApplicationDbContext(OptionsBuilder.Options)
                CN = _DB.Database.GetDbConnection()
                CN.Open()
                Sql.ExecNonQuery(_DB, $"INSERT INTO `DockerEvents`(`toDockerHub`,`j`,`Event`) VALUES ({DockerHubVm},{e.Key},'{Lines(i).Replace("'", "^").Replace("`", "^")}');")
                If SendNotification IsNot Nothing Then
                    If SendBackendNotification Then
                        SendNotification.Invoke(sender.ToString, Lines(i))
                    End If
                End If
            End If
        Next
    End Sub

    '2022-09-10T09:14:36.909917942+02:00 container create ecfdaa3352c22c5f8b851d2d24cc00c1b56108aacaeb67931322dd6c573ccc19 (image=busybox, name=busybox.2022-09-10-09.14.36.d5002f29-f7e8-48b8-bd4e-c9e6e6c4397c)
    '2022-09-10T17:56:46.731982177+02:00 container die a0fd089078f9974df0d4d28053adb4e3091701ec1b06bb1d3dc44a3b00bf733f (exitCode=0, image=busybox, name=busybox.2022-09-10-17.56.46.d717cc89-77b7-425d-baea-45690bbb5e96)
    Sub Inspect(sender As Object, e As String)
        Dim Items As String() = e.Split(" ")
        If Items.Count >= 0 Then
            If Items(1) = "container" And Items(2) = "start" Then
                SendNotification(ContainerStart, Items(3), Items(5).Replace(vbLf, ""), Items(0).Replace(vbLf, ""))
            ElseIf Items(1) = "container" And Items(2) = "die" Then
                SendNotification(ContainerStop, Items(3), Items(6).Replace(vbLf, ""), Items(0).Replace(vbLf, ""))
            End If
        End If
    End Sub

    Private NotificationTokenLogin As String
    Private NotificationTokenPass As String
    Private Request As MyWebClient
    Private Counter As Integer
    Private RequestWebRequest As New Object
    Sub SendNotification(URL As String, ContainerID As String, Name As String, Time As String)
        Task.Run(Sub()
                     Interlocked.Increment(Counter)
                     SyncLock RequestWebRequest
                         Dim FullUrl As String = $"{URL}?ID={ContainerID}&{Name.Replace(")", "")}&Time={Time}&N={Counter}"
                         Try
                             Request = New MyWebClient
                             Request.BaseAddress = BackendBaseURL
                             Request.Headers.Add("Content-Type", "application/json")
                             Dim Token = GetNotificationToken(Request, NotificationTokenLogin, NotificationTokenPass)
                             Request.Headers.Clear()
                             Request.Headers.Add("Authorization", "Bearer: " & Token)
                             Request.Headers.Add("Content-Type", "application/json")
                             While (Request.IsBusy)
                                 Task.Delay(1000).Wait()
                             End While
                             Dim Response As String = Request.DownloadString(FullUrl)
                             Console.WriteLine($"Notification {Request.BaseAddress}/{FullUrl} - {Response}") '"OkObjectResult"/"ObjectResult"
                         Catch ex As Exception
                             Console.WriteLine($"Notification {Request.BaseAddress}/{FullUrl} Failed. {ex.Message}")
                         End Try
                     End SyncLock
                 End Sub)
    End Sub
End Module

Class TmpVmAccess
    Property AdminLogin As String
    Property AdminPassword As String
    Property Name As String
End Class
