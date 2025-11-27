Imports System.IO

Public Class MainForm
    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            ' Ensure native SQLite DLLs are discoverable before opening any connections
            SQLiteIF.InitializeNative()

            Dim solutionRoot = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", ".."))
            Dim dbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "chinook.db")

            If Not File.Exists(dbFile) Then
                Throw New FileNotFoundException($"Database file not found at: {dbFile}")
            End If

            Dim db = New SQLiteIF(dbFile)
            db.EnsureDatabaseOpenable()

            Dim dt = db.QueryDataTable("SELECT AlbumId, Title, ArtistId FROM albums")

            ' Bind the DataTable to the DataGridView
            AlbumsView.AutoGenerateColumns = True
            AlbumsView.ReadOnly = True
            AlbumsView.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            AlbumsView.MultiSelect = False
            AlbumsView.DataSource = dt

            For Each row As DataRow In dt.Rows
                Dim albumId = If(row("AlbumId") Is DBNull.Value, 0, CInt(row("AlbumId")))
                Dim title = If(row("Title") Is DBNull.Value, "", CStr(row("Title")))
                Dim artistId = If(row("ArtistId") Is DBNull.Value, 0, CInt(row("ArtistId")))
                Console.WriteLine($"{albumId} - {title} (Artist {artistId})")
                Debug.WriteLine($"{albumId} - {title} (Artist {artistId})")
            Next

        Catch ex As Exception
            MessageBox.Show($"Startup error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ' Keep the form open so you can see the UI; remove the following line unless you want to exit
            ' Me.Close()
        End Try



    End Sub
End Class
