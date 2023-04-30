Imports System.Runtime.CompilerServices
Imports ZoppaSimpleCommunication

Module Program
    Sub Main(args As String())
        Using srv = SimpleServer.ServerRun(8081, New Logger())
            AddHandler srv.ReceivedMessage,
            Sub(req, res)
                res.ValueType = DataType.StringType
                res.ValueString = "éÛêMÇµÇ‹ÇµÇΩÅB"
            End Sub
            Console.ReadLine()
        End Using
    End Sub

    Class Logger
        Implements ZoppaSimpleCommunication.ILogger

        Private ReadOnly mLogger As ZoppaLogger.Logger
        Public Sub New()
            Me.mLogger = ZoppaLogger.Logger.Use("server.log")
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
