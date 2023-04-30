Option Strict On
Option Explicit On

Imports System.Net
Imports System.Net.Sockets
Imports System.Text

''' <summary>シンプルなTPC/IPサーバー機能です。</summary>
Public NotInheritable Class SimpleServer
    Implements IDisposable

    ''' <summary>クライアントからメッセージ受信を処理します。</summary>
    ''' <param name="request">受信メッセージ。</param>
    ''' <param name="response">返信メッセージ。</param>
    Public Event ReceivedMessage(request As Communication, response As Communication)

    ' TCPリスナー
    Private ReadOnly mListener As TcpListener

    ' ログ出力機能
    Private ReadOnly mLogger As ILogger

    ' クライアントタスク
    Private ReadOnly mClientTasks As New HashSet(Of TcpClient)()

    ' 実行中フラグ
    Private mRunning As Boolean

    ' 実行タスク
    Private mTask As Task

    ' 破棄済みフラグ
    Private mDisposed As Boolean

    ''' <summary>コンストラクタ。</summary>
    ''' <param name="listner">TCPリスナー。</param>
    ''' <param name="logger">ログ出力機能。</param>
    Private Sub New(listner As TcpListener, logger As ILogger)
        Me.mListener = listner
        Me.mLogger = logger
        Me.mRunning = True
        Me.mDisposed = False
    End Sub

    ''' <summary>サーバー機能を開始します。</summary>
    ''' <param name="servicePort">サービスポート。</param>
    ''' <param name="logger">ログ出力機能（指定がなければデフォルト出力）</param>
    ''' <returns>サーバー機能。</returns>
    Public Shared Function ServerRun(servicePort As Integer,
                                     Optional ByVal logger As ILogger = Nothing) As SimpleServer
        ' ログ出力機能の初期化
        Try
            If logger Is Nothing Then
                logger = New MyLogger()
            End If
        Catch ex As Exception
            Throw New InvalidOperationException($"Failed to initialize the log output function:{ex.Message}")
        End Try

        Try
            Dim listener = New TcpListener(IPAddress.Any, servicePort)
            Dim self As New SimpleServer(listener, logger)

            logger?.LoggingInformation($"TCP/IP server start by port:{servicePort}")
            listener.Start()

            ' 別スレッドでポートを監視
            self.mTask = Task.Factory.StartNew(
                Sub()
                    self.ListenMethod()
                End Sub,
                TaskCreationOptions.LongRunning
            )
            Return self

        Catch ex As Exception
            Throw New InvalidOperationException($"Failed to start server function:{ex.Message}")
        End Try
    End Function

    ''' <summary>リソースの解放を行う。</summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Me.mLogger?.LoggingInformation("Dispposing server")
        Me.StopServer()
    End Sub

    ''' <summary>サーバー機能を停止します。</summary>
    Public Sub StopServer()
        If Not Me.mDisposed Then
            SyncLock Me
                Me.mRunning = False
            End SyncLock

            Try
                ' クライアントタスクを停止
                Me.mLogger?.LoggingInformation("Stop client tasks")
                SyncLock Me
                    For Each client In Me.mClientTasks
                        client.Close()
                    Next
                    Me.mClientTasks.Clear()
                End SyncLock

                ' リスナーを停止
                Me.mLogger?.LoggingInformation("Stop listner")
                Me.mListener.Stop()

                ' タスクを停止
                Me.mLogger?.LoggingInformation("Stop main task")
                Me.mTask.Wait()
                Me.mTask.Dispose()

                Me.mDisposed = True

            Catch ex As Exception
                Me.mLogger?.LoggingError($"Failed to stop server function:{ex.Message}")
            End Try
        End If
    End Sub

    ''' <summary>TCP/IPポートの監視を実行します。</summary>
    Private Sub ListenMethod()
        Do While True
            ' 実行フラグがオフになればループ終了
            SyncLock Me
                If Not Me.mRunning Then
                    Exit Do
                End If
            End SyncLock

            Try
                ' -----------------------------------------
                ' クライアントからメッセージを受信します
                ' -----------------------------------------
                Dim useAddress As IPAddress
                Dim useCommand As Integer = 0
                Dim usePort As UShort = 0

                Using client = Me.mListener.AcceptTcpClient()
                    ' クライアントの IPアドレスを取得
                    useAddress = CType(client.Client.RemoteEndPoint, IPEndPoint).Address

                    Using stream = client.GetStream()
                        ' コマンドを取得
                        useCommand = stream.ReadByte()

                        ' コマンドごとに処理分け
                        Select Case useCommand
                            Case COMMAND_HELLO
                                usePort = ReadUInt16(stream)
                                Me.mLogger?.LoggingInformation($"Hello command from {useAddress}:{usePort}")

                            Case Else
                                Me.mLogger?.LoggingError($"Unknown command({useCommand:X}) from {useAddress}")
                        End Select
                    End Using
                End Using

                ' ---------------------------
                ' 応答スレッドを開始します
                ' ---------------------------
                Dim resTask = Task.Run(
                    Sub()
                        Me.ClientTaskThread(useAddress, usePort)
                    End Sub
                )

            Catch ex As SocketException
                If ex.ErrorCode <> 10004 Then
                    Me.mLogger?.LoggingError($"ソケットエラー:{ex.Message} ※ サービス終了時にも出力されるため注意")
                    Me.mLogger?.LoggingError(ex)
                End If

            Catch ex As Exception
                Me.mLogger?.LoggingError($"TCP/IP サーバーエラー：{ex.Message}")
                Me.mLogger?.LoggingError(ex)
            End Try

            Threading.Thread.Sleep(10)
        Loop
    End Sub

    ''' <summary>クライアント通信を実行します。</summary>
    ''' <param name="useAddress">クライアントのアドレス。</param>
    ''' <param name="usePort">クライアントのポート。</param>
    Private Sub ClientTaskThread(useAddress As IPAddress, usePort As UShort)
        Me.mLogger?.LoggingInformation($"Start communication by thread id:{Threading.Thread.CurrentThread.ManagedThreadId}")

        ' 公開、秘密鍵を作成
        Dim keys = RsaCreateKeys()
        Me.mLogger?.LoggingInformation($"Create RSA keys:{keys.publicKey}")

        Try
            Using client As New TcpClient(useAddress.ToString(), usePort)
                SyncLock Me
                    Me.mClientTasks.Add(client)
                End SyncLock

                Using stream = client.GetStream()
                    ' 公開キーを送信
                    Me.mLogger?.LoggingInformation($"Send public key to {useAddress}:{usePort}")
                    WriteString(stream, keys.publicKey)
                    stream.Flush()

                    ' 共通鍵を受信
                    Dim commonKeyLen = ReadInteger(stream)
                    Dim commonKeyData = ReadBytes(stream, commonKeyLen)
                    Dim commonKeys = RsaDecrypt(commonKeyData, keys.privateKey).Split(":"c)
                    Dim aseKey = Encoding.UTF8.GetBytes(commonKeys(0))
                    Dim aseIv = Encoding.UTF8.GetBytes(commonKeys(1))
                    Me.mLogger?.LoggingInformation($"receive common key from {useAddress}:{usePort}")

                    ' メッセージ待機
                    Using aesLapper As New AesLapper(aseKey, aseIv)
                        Do While True
                            Dim dataLen = ReadInteger(stream)
                            If dataLen >= 0 Then
                                ' -----------------------
                                ' 受信メッセージを処理
                                ' -----------------------
                                Dim no = stream.ReadByte()
                                Dim data As Byte()

                                Dim request As New Communication()
                                Dim response As New Communication() With {.ValueType = DataType.IntegerType}
                                Select Case no
                                    Case DataType.ExitType
                                        ' 終了コマンドを受信
                                        Exit Do

                                    Case DataType.IntegerType
                                        ' 整数値を受信
                                        data = ReadBytes(stream, dataLen - 1)
                                        Dim inum = BitConverter.ToInt32(data, 0)
                                        Me.mLogger?.LoggingDebug($"receive from {useAddress}:{usePort} length:{dataLen} integer:{inum}")

                                        request.ValueType = DataType.IntegerType
                                        request.ValueInteger = inum
                                        RaiseEvent ReceivedMessage(request, response)

                                    Case DataType.StringType
                                        ' 文字列を受信
                                        data = aesLapper.Decrypt(ReadBytes(stream, dataLen - 1))
                                        Dim message = Encoding.UTF8.GetString(data)
                                        Me.mLogger?.LoggingDebug($"receive from {useAddress}:{usePort} length:{dataLen} string:{If(message.Length < 25, message, message.Substring(0, 25) & "...")}")

                                        request.ValueType = DataType.StringType
                                        request.ValueString = message
                                        RaiseEvent ReceivedMessage(request, response)

                                    Case DataType.BytesType
                                        ' バイト配列を受信
                                        data = aesLapper.Decrypt(ReadBytes(stream, dataLen - 1))
                                        Me.mLogger?.LoggingDebug($"receive from {useAddress}:{usePort} length:{dataLen} bytes:{GeBytesString(data)}")

                                        request.ValueType = DataType.BytesType
                                        request.ValueBytes = data
                                        RaiseEvent ReceivedMessage(request, response)
                                End Select

                                ' --------------------
                                ' メッセージを返信
                                ' --------------------
                                Select Case response.ValueType
                                    Case DataType.IntegerType
                                        Dim inum = If(response.ValueInteger, 0)
                                        Me.mLogger?.LoggingDebug($"send to {useAddress}:{usePort} integer:{inum}")
                                        CommunicationWrite(stream, inum)

                                    Case DataType.StringType
                                        Dim message = If(response.ValueString, "")
                                        Me.mLogger?.LoggingDebug($"send to {useAddress}:{usePort} string:{If(message.Length < 50, message, message.Substring(0, 50))}")
                                        CommunicationWrite(stream, aesLapper, message)
                                End Select
                            Else
                                Me.mLogger?.LoggingInformation($"connect close {useAddress}:{usePort}")
                                Exit Do
                            End If
                        Loop
                    End Using
                End Using

                SyncLock Me
                    Me.mClientTasks.Remove(client)
                End SyncLock
            End Using

        Catch ex As Exception
            Me.mLogger?.LoggingError($"TCP/IP サーバーエラー：{ex.Message} thread id:{Threading.Thread.CurrentThread.ManagedThreadId}")
            Me.mLogger?.LoggingError(ex)
        End Try
    End Sub

    ''' <summary>バイト配列をログに出力する文字列を取得します。</summary>
    ''' <param name="datas">バイト配列。</param>
    ''' <returns>ログ文字列。</returns>
    Private Shared Function GeBytesString(datas As Byte()) As String
        Dim res As New StringBuilder()

        Dim maxLen = Math.Min(datas.Length, 25)
        For i As Integer = 0 To maxLen - 2
            res.AppendFormat("{0:X2},", datas(i))
        Next
        res.AppendFormat("{0:X2}", datas(maxLen - 1))
        If datas.Length > 25 Then
            res.Append("...")
        End If

        Return res.ToString()
    End Function

End Class
