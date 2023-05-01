Option Strict On
Option Explicit On

Imports System.Runtime.CompilerServices

''' <summary>ログ出力機能を提供します。</summary>
Public Interface ILogger

    ''' <summary>デバッグログを出力します。</summary>
    ''' <param name="message">ログメッセージ。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Sub LoggingDebug(message As String,
                     <CallerMemberName> Optional memberName As String = "",
                     <CallerLineNumber> Optional lineNo As Integer = 0)

    ''' <summary>情報ログを出力します。</summary>
    ''' <param name="message">ログメッセージ。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Sub LoggingInformation(message As String,
                           <CallerMemberName> Optional memberName As String = "",
                           <CallerLineNumber> Optional lineNo As Integer = 0)

    ''' <summary>エラーログを出力します。</summary>
    ''' <param name="message">ログメッセージ。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Sub LoggingError(message As String,
                     <CallerMemberName> Optional memberName As String = "",
                     <CallerLineNumber> Optional lineNo As Integer = 0)

    ''' <summary>エラーログを出力します。</summary>
    ''' <param name="ex">例外情報。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Sub LoggingError(ex As Exception,
                     <CallerMemberName> Optional memberName As String = "",
                     <CallerLineNumber> Optional lineNo As Integer = 0)

End Interface

''' <summary>デフォルトログ出力機能を提供します。</summary>
Friend NotInheritable Class MyLogger
    Implements ILogger

    ''' <summary>コンストラクタ。</summary>
    Public Sub New()

    End Sub

    ''' <summary>デバッグログを出力します。</summary>
    ''' <param name="message">ログメッセージ。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Public Sub LoggingDebug(message As String,
                            <CallerMemberName> Optional memberName As String = "",
                            <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingDebug
        Console.Out.WriteLine($"{Date.Now:yyyy/MM/dd HH:mm:ss.fff} [DEBUG] {message}")
    End Sub

    ''' <summary>情報ログを出力します。</summary>
    ''' <param name="message">ログメッセージ。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Public Sub LoggingInformation(message As String,
                                  <CallerMemberName> Optional memberName As String = "",
                                  <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingInformation
        Console.Out.WriteLine($"{Date.Now:yyyy/MM/dd HH:mm:ss.fff} [INFO]  {message}")
    End Sub

    ''' <summary>エラーログを出力します。</summary>
    ''' <param name="message">ログメッセージ。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Public Sub LoggingError(message As String,
                            <CallerMemberName> Optional memberName As String = "",
                            <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingError
        Console.Out.WriteLine($"{Date.Now:yyyy/MM/dd HH:mm:ss.fff} [ERROR] {message}")
    End Sub

    ''' <summary>エラーログを出力します。</summary>
    ''' <param name="ex">例外情報。</param>
    ''' <param name="memberName">メソッド名。</param>
    ''' <param name="lineNo">行番号。</param>
    Public Sub LoggingError(ex As Exception,
                            <CallerMemberName> Optional memberName As String = "",
                            <CallerLineNumber> Optional lineNo As Integer = 0) Implements ILogger.LoggingError
        Console.Out.WriteLine($"{Date.Now:yyyy/MM/dd HH:mm:ss.fff} [ERROR] {ex} {ex.StackTrace}")
    End Sub

End Class