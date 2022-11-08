Imports System.Data.Common
Imports BackendAPI.Model
Imports Microsoft.EntityFrameworkCore
Imports MySqlConnector

Namespace Helper
    Public Module Sql
        <Runtime.CompilerServices.Extension>
        Public Function ExecNonQuery(_DB As ApplicationDbContext, SQL As String, Optional Transaction As Data.Common.DbTransaction = Nothing) As Integer
            Dim CMD1 = _DB.Database.GetDbConnection().CreateCommand()
            CMD1.CommandText = SQL
            If Transaction IsNot Nothing Then
                CMD1.Transaction = Transaction
            End If
            Return CMD1.ExecuteNonQuery()
        End Function

        Public Function ExecRDR(Of T)(_DB As ApplicationDbContext, SQL As String, RowMapperFunc As Func(Of DbDataReader, T), Optional Transaction As Data.Common.DbTransaction = Nothing) As List(Of T)
            Dim Ret1 As New List(Of T)
            Dim CMD1 = _DB.Database.GetDbConnection().CreateCommand()
            CMD1.CommandText = SQL
            If Transaction IsNot Nothing Then
                CMD1.Transaction = Transaction
            End If
            Dim RDR1 = CMD1.ExecuteReader
            While RDR1.Read
                Dim X = RowMapperFunc(RDR1)
                Ret1.Add(X)
            End While
            RDR1.Close()
            Return Ret1
        End Function

        Public Async Function ExecNonQueryAsync(_DB As ApplicationDbContext, SQL As String, Optional Transaction As Data.Common.DbTransaction = Nothing) As Task(Of Integer)
            Try
                Dim EF_CN As DbConnection = _DB.Database.GetDbConnection()
                Using CN = New MySqlConnection(EF_CN.ConnectionString)
                    Await CN.OpenAsync
                    Using CMD = CN.CreateCommand
                        CMD.CommandText = SQL
                        If Transaction IsNot Nothing Then
                            CMD.Transaction = Transaction
                        End If
                        Dim Ret = CMD.ExecuteNonQueryAsync
                        Await Ret
                        Return Ret.Result
                    End Using
                End Using
            Catch ex As Exception
                Console.WriteLine(ex.Message & vbCrLf & SQL)
            End Try
        End Function

        Public Function ExecRDR(Of T)(CN As MySqlConnection, SQL As String, RowMapperFunc As Func(Of DbDataReader, T), Optional Transaction As Data.Common.DbTransaction = Nothing) As List(Of T)
            Dim Ret1 As New List(Of T)
            ReOpenMySQL(CN)
            Dim CMD1 = CN.CreateCommand()
            CMD1.CommandText = SQL
            If Transaction IsNot Nothing Then
                CMD1.Transaction = Transaction
            End If
            Dim RDR1 = CMD1.ExecuteReader
            While RDR1.Read
                Dim X = RowMapperFunc(RDR1)
                Ret1.Add(X)
            End While
            RDR1.Close()
            Return Ret1
        End Function

        Public Function ReOpenMySQL(ByRef CN As MySqlConnection) As Boolean
            If CN Is Nothing Then
Open:
                CN = New MySqlConnection(CN.ConnectionString)
                Try
                    CN.Open()
                    If CN.State = Data.ConnectionState.Open Then
                        Return True
                    Else
                        Return False
                    End If
                Catch ex As Exception
                    Console.WriteLine(ex.Message & vbCrLf & CN.ConnectionString)
                    Return False
                End Try
            Else
                If CN.State = Data.ConnectionState.Open Then
                    Return True
                Else
                    GoTo Open
                End If
            End If
        End Function

    End Module
End Namespace