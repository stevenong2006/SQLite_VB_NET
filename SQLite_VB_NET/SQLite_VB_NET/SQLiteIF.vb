Imports System
Imports System.Data
Imports System.Data.SQLite
Imports System.IO
Imports System.Runtime.InteropServices

Public Class SQLiteIF
    Private ReadOnly _dbPath As String
    Private ReadOnly _connectionString As String

    Public Sub New(dbPath As String)
        If String.IsNullOrWhiteSpace(dbPath) Then
            Throw New ArgumentException("dbPath cannot be null or empty.", NameOf(dbPath))
        End If

        _dbPath = dbPath

        Dim dir = Path.GetDirectoryName(_dbPath)
        If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
            Directory.CreateDirectory(dir)
        End If

        Dim csb As New SQLiteConnectionStringBuilder() With {
            .DataSource = _dbPath,
            .Version = 3,
            .ForeignKeys = True,
            .JournalMode = SQLiteJournalModeEnum.Wal,
            .Pooling = True
        }
        _connectionString = csb.ToString()
    End Sub

    Public ReadOnly Property ConnectionString As String
        Get
            Return _connectionString
        End Get
    End Property

    ' Ensures the DB file can be created/opened (creates directories if needed).
    Public Sub EnsureDatabaseOpenable()
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()
        End Using
    End Sub

    Public Function ExecuteNonQuery(sql As String, Optional parameters As IEnumerable(Of SQLiteParameter) = Nothing) As Integer
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()
            Using cmd As New SQLiteCommand(sql, conn)
                If parameters IsNot Nothing Then AddParameters(cmd, parameters)
                Return cmd.ExecuteNonQuery()
            End Using
        End Using
    End Function

    Public Function ExecuteScalar(Of T)(sql As String, Optional parameters As IEnumerable(Of SQLiteParameter) = Nothing) As T
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()
            Using cmd As New SQLiteCommand(sql, conn)
                If parameters IsNot Nothing Then AddParameters(cmd, parameters)
                Dim result = cmd.ExecuteScalar()
                If result Is Nothing OrElse result Is DBNull.Value Then
                    Return Nothing
                End If
                Return CType(Convert.ChangeType(result, GetType(T)), T)
            End Using
        End Using
    End Function

    Public Function QueryDataTable(sql As String, Optional parameters As IEnumerable(Of SQLiteParameter) = Nothing) As DataTable
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()
            Using cmd As New SQLiteCommand(sql, conn)
                If parameters IsNot Nothing Then AddParameters(cmd, parameters)
                Using rdr = cmd.ExecuteReader()
                    Dim dt As New DataTable()
                    dt.Load(rdr)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    Public Function Query(Of T)(sql As String, map As Func(Of SQLiteDataReader, T), Optional parameters As IEnumerable(Of SQLiteParameter) = Nothing) As List(Of T)
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()
            Using cmd As New SQLiteCommand(sql, conn)
                If parameters IsNot Nothing Then AddParameters(cmd, parameters)
                Using rdr = cmd.ExecuteReader()
                    Dim list As New List(Of T)()
                    While rdr.Read()
                        list.Add(map(rdr))
                    End While
                    Return list
                End Using
            End Using
        End Using
    End Function

    ' Execute multiple commands atomically.
    Public Sub ExecuteInTransaction(actions As Action(Of SQLiteConnection, SQLiteTransaction))
        Using conn As New SQLiteConnection(_connectionString)
            conn.Open()
            Using tx = conn.BeginTransaction()
                Try
                    actions(conn, tx)
                    tx.Commit()
                Catch
                    Try : tx.Rollback() : Catch : End Try
                    Throw
                End Try
            End Using
        End Using
    End Sub

    ' Helper to create parameters.
    Public Shared Function Param(name As String, value As Object, Optional dbType As Nullable(Of DbType) = Nothing) As SQLiteParameter
        If Not name.StartsWith("@") Then name = "@" & name
        Dim p As New SQLiteParameter(name, If(value, DBNull.Value))
        If dbType.HasValue Then p.DbType = dbType.Value
        Return p
    End Function

    Private Shared Sub AddParameters(cmd As SQLiteCommand, parameters As IEnumerable(Of SQLiteParameter))
        For Each p In parameters
            cmd.Parameters.Add(p)
        Next
    End Sub

    ' Entry point to initialize native SQLite DLL discovery for the current process.
    Public Shared Sub InitializeNative()
        EnsureNativeSqliteDllsOnPath()
    End Sub

    Private Shared Sub EnsureNativeSqliteDllsOnPath()
        Dim baseDir = AppDomain.CurrentDomain.BaseDirectory

        ' Candidate native directories commonly used by NuGet packages:
        ' - Microsoft.Data.Sqlite / SQLitePCLRaw: runtimes\win-x64\native or runtimes\win-x86\native
        ' - System.Data.SQLite: x64 or x86 with SQLite.Interop.dll
        Dim is64 = (IntPtr.Size = 8)
        Dim candidates As New List(Of String)

        ' SQLitePCLRaw bundle locations
        Dim pclNative = Path.Combine(baseDir, "runtimes", If(is64, "win-x64", "win-x86"), "native")
        candidates.Add(pclNative)

        ' System.Data.SQLite typical layout
        candidates.Add(Path.Combine(baseDir, If(is64, "x64", "x86")))

        ' Also consider direct drop in baseDir
        candidates.Add(baseDir)

        ' Append existing directories to PATH if they contain expected DLLs
        Dim dllNames = New String() {"e_sqlite3.dll", "SQLite.Interop.dll"}
        Dim addedPath As Boolean = False

        For Each candidateDir As String In candidates
            If Directory.Exists(candidateDir) Then
                For Each dllName As String In dllNames
                    Dim p = Path.Combine(candidateDir, dllName)
                    If File.Exists(p) Then
                        AppendToProcessPath(candidateDir)
                        addedPath = True
                        Exit For
                    End If
                Next
            End If
        Next

        ' Try to preload one of the native DLLs to surface any issue early
        If addedPath Then
            PreloadNativeDllIfPresent(Path.Combine(pclNative, "e_sqlite3.dll"))
            PreloadNativeDllIfPresent(Path.Combine(Path.Combine(baseDir, If(is64, "x64", "x86")), "SQLite.Interop.dll"))
        End If
    End Sub

    Private Shared Sub AppendToProcessPath(dir As String)
        Dim current = Environment.GetEnvironmentVariable("PATH")
        If Not String.IsNullOrEmpty(current) AndAlso current.IndexOf(dir, StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return
        End If
        Dim newPath = If(String.IsNullOrEmpty(current), dir, current & ";" & dir)
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process)
    End Sub

    <DllImport("kernel32", SetLastError:=True, CharSet:=CharSet.Unicode)>
    Private Shared Function LoadLibrary(lpFileName As String) As IntPtr
    End Function

    Private Shared Sub PreloadNativeDllIfPresent(dllPath As String)
        Try
            If File.Exists(dllPath) Then
                Dim handle = LoadLibrary(dllPath)
                ' handle == IntPtr.Zero means load failed; let later code raise the usual exception
            End If
        Catch
            ' Swallow; actual connection open will throw a meaningful exception if not loadable
        End Try
    End Sub
End Class