Imports BackendAPI.Docker
Imports Microsoft.EntityFrameworkCore

Namespace Model

    Public Class ApplicationDbContext
        Inherits DbContext

        Public Sub New(options As DbContextOptions(Of ApplicationDbContext))
            MyBase.New(options)
        End Sub

        'Public Property DockerHubList As DbSet(Of OneDockerInfo)

    End Class
End Namespace