Imports System.IO

Public Class MainForm
    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            ' Ensure native SQLite DLLs are discoverable before opening any connections
            SQLiteIF.InitializeNative()

            Dim solutionRoot = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", ".."))
            Dim dbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "chinook.db")

            If Not File.Exists(dbFile) Then
                MessageBox.Show($"Database file not found at: {dbFile}", "Missing Database", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Throw New FileNotFoundException($"Database file not found at: {dbFile}")
            End If

            Dim db = New SQLiteIF(dbFile)
            db.EnsureDatabaseOpenable()

            Dim intoTheLight As DataTable = db.QueryDataTable("SELECT AlbumId, Title, ArtistId FROM albums WHERE AlbumId = 40")

            Console.WriteLine($"intoTheLight.Rows.Count = {intoTheLight.Rows.Count}")
            Debug.WriteLine($"intoTheLight.Rows.Count = {intoTheLight.Rows.Count}")

            Dim dt = db.QueryDataTable("SELECT AlbumId, Title, ArtistId FROM albums")

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

            ' EXPLANATION:
            ' The generic method ExecuteScalar(Of T) could not infer T from the call.
            ' Specify a concrete type argument. Using Object preserves existing DBNull checks.
            Dim maxAlbumIdObj = db.ExecuteScalar(Of Object)("SELECT MAX(AlbumId) FROM albums")
            Dim maxAlbumId = If(maxAlbumIdObj Is Nothing OrElse maxAlbumIdObj Is DBNull.Value, 0, Convert.ToInt32(maxAlbumIdObj))
            Console.WriteLine($"Max AlbumId = {maxAlbumId}")
            Debug.WriteLine($"Max AlbumId = {maxAlbumId}")

            ' Update album title by ID (do this before reloading the DataTable so the grid reflects it)
            Dim albumIdToUpdate As Integer = 40
            Dim newTitle As String = "Into The Light (Remastered)"

            Dim rowsAffected = db.ExecuteNonQuery(
                "UPDATE albums SET Title = @title WHERE AlbumId = @id",
                New System.Data.SQLite.SQLiteParameter() {
                    SQLiteIF.Param("title", newTitle),
                    SQLiteIF.Param("id", albumIdToUpdate)
                })

            Debug.WriteLine($"Rows updated: {rowsAffected}")
            Console.WriteLine($"Rows updated: {rowsAffected}")

            ' Verify the update and refresh the grid
            Dim verifyTitle As Object = db.ExecuteScalar(Of Object)(
                "SELECT Title FROM albums WHERE AlbumId = @id",
                New System.Data.SQLite.SQLiteParameter() {
                    SQLiteIF.Param("id", albumIdToUpdate)
                })
            Console.WriteLine($"Verified Title: {verifyTitle}")

            ' Reload data so UI shows the change
            dt = db.QueryDataTable("SELECT AlbumId, Title, ArtistId FROM albums")
            AlbumsView.DataSource = dt

        Catch ex As Exception
            MessageBox.Show($"Startup error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
