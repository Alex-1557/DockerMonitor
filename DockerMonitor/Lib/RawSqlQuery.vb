
Imports System.Data
Imports System.Data.Common
Imports BackendAPI.Model
Imports Microsoft.EntityFrameworkCore

Namespace Helper
    Public Module RawSqlQuery
        <Runtime.CompilerServices.Extension>
        Public Function RawSqlQuery(Of T)(Context As ApplicationDbContext, ByVal SqlQuery As String, ByVal RowMapperFunc As Func(Of DbDataReader, T)) As Tuple(Of List(Of T), Exception)
            Try
                Using CN = Context.Database.GetDbConnection()
                    Using Command = CN.CreateCommand()
                        Command.CommandText = SqlQuery
                        Command.CommandType = CommandType.Text
                        Context.Database.OpenConnection()

                        Using RDR = Command.ExecuteReader()
                            Dim ResultList = New List(Of T)()

                            While RDR.Read()
                                ResultList.Add(RowMapperFunc(RDR))
                            End While

                            RDR.Close()
                            Return New Tuple(Of List(Of T), Exception)(ResultList, Nothing)
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                'Debug only, because this function show password in AES_DECRYPT()
                Debug.WriteLine(ex.Message & " : " & SqlQuery)
                Return New Tuple(Of List(Of T), Exception)(Nothing, ex)
            End Try
        End Function

    End Module
End Namespace