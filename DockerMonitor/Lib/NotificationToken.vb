Imports System.Text
Imports DockerMonitor.Model
Imports Newtonsoft.Json

Public Module NotificationToken
    Public Function GetNotificationToken(Request As MyWebClient, Username As String, Password As String) As String
        Try
            Dim PostPrm = New AuthenticateRequest With {
                .Username = Username,
                .Password = Password
                }
            Dim PostData = JsonConvert.SerializeObject(PostPrm)
            'WebClient does not support concurrent I/O operations.
            While (Request.IsBusy)
                System.Threading.Thread.Sleep(Random.Shared.Next(1000))
            End While
            Dim Response = Encoding.UTF8.GetString(Request.UploadData("/Users/Authenticate", Encoding.UTF8.GetBytes(PostData)))
            Dim Ret1 = JsonConvert.DeserializeObject(Response)
            Return Ret1("token").ToString
        Catch ex As Exception
            Debug.WriteLine(ex.Message)
        End Try
    End Function
End Module
