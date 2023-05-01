Imports System.Runtime.CompilerServices
Imports ZoppaSimpleCommunication

Module Program
    Sub Main(args As String())
        Task.Factory.StartNew(
            Sub()
                For i As Integer = 1 To 10
                    Using client = SimpleClient.ClientRun("127.0.0.1", 8081, logger:=New Logger())
                        Dim a0 = client.Send(New Byte() {0, 1, 2, 3, 4, 5, 6, 7, 8, 9})
                        Console.WriteLine(a0.ValueString)

                        Dim a1 = client.Send(0)
                        Console.WriteLine(a1.ValueString)

                        Dim a2 = client.Send("文字列送信テスト")
                        Console.WriteLine(a2.ValueString)

                        Dim a3 = client.Send("地獄楽視聴中")
                        Console.WriteLine(a3.ValueString)
                    End Using

                    Threading.Thread.Sleep(500)
                Next
            End Sub,
            TaskCreationOptions.LongRunning
        ).Wait()
    End Sub

    Class Logger
        Implements ZoppaSimpleCommunication.ILogger

        Private ReadOnly mLogger As ZoppaLogger.Logger
        Public Sub New()
            Me.mLogger = ZoppaLogger.Logger.Use("client.log")
        End Sub

        Public Sub LoggingDebug(message As String, <CallerMemberName> Optional memberName As String = "", <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingDebug
            Me.mLogger.LoggingDebug(message, memberName, lineNo)
        End Sub

        Public Sub LoggingInformation(message As String, <CallerMemberName> Optional memberName As String = "", <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingInformation
            Me.mLogger.LoggingInformation(message, memberName, lineNo)
        End Sub

        Public Sub LoggingError(message As String, <CallerMemberName> Optional memberName As String = "", <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingError
            Me.mLogger.LoggingError(message, memberName, lineNo)
        End Sub

        Public Sub LoggingError(ex As Exception, <CallerMemberName> Optional memberName As String = "", <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingError
            Me.mLogger.LoggingError(ex, memberName, lineNo)
        End Sub

    End Class

End Module
