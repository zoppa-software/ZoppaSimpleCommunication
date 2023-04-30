Option Strict On
Option Explicit On
Imports System.Net
Imports System.Net.Sockets
Imports System.Text

''' <summary>シンプルなTPC/IPサーバークライアント機能です。</summary>
Public NotInheritable Class SimpleClient
    Implements IDisposable

    Private ReadOnly mListener As TcpListener

    Private ReadOnly mLogger As ILogger

    Private ReadOnly mRequests As New Queue(Of (req As LocalComm, res As Communication))

    ' 実行中フラグ
    Private mRunning As Boolean

    ' 実行タスク
    Private mTask As Task

    ' 破棄済みフラグ
    Private mDisposed As Boolean

    ''' <summary>コンストラクタ。</summary>
    ''' <param name="listener">TCPリスナー。</param>
    ''' <param name="logger">ログ出力機能。</param>
    Private Sub New(listener As TcpListener, logger As ILogger)
        Me.mLogger = logger
        Me.mListener = listener
        Me.mRunning = True
        Me.mDisposed = False
    End Sub

    ''' <summary>リソースの解放を行う。</summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Me.mLogger?.LoggingInformation("Dispposing client")
        Me.StopClient()
    End Sub

    ''' <summary>クライアント機能を停止します。</summary>
    Public Sub StopClient()
        If Not Me.mDisposed Then
            Do While True
                SyncLock Me
                    If Me.mRequests.Count <= 0 Then Exit Do
                End SyncLock
                Threading.Thread.Sleep(10)
            Loop
            SyncLock Me
                Me.mRunning = False
            End SyncLock
            Try
                ' タスクを停止
                Me.mLogger?.LoggingInformation("Stop main task")
                Me.mTask.Wait()
                Me.mTask.Dispose()

                Me.mDisposed = True

            Catch ex As Exception
                Me.mLogger?.LoggingError($"Failed to stop client function:{ex.Message}")
            End Try
        End If
    End Sub

    ''' <summary>ホスト名のIPアドレスを取得します。</summary>
    ''' <param name="hostName">ホスト名。</param>
    ''' <returns>IPアドレス。</returns>
    Private Shared Function ParseIPAddress(ByVal hostName As String) As IPAddress
        ' IPアドレスとして変換可能ならばその値、そうでなければDNSに問い合わせます
        Dim ipaddr As IPAddress = Nothing
        Try
            ipaddr = IPAddress.Parse(hostName)
        Catch ex As Exception
            Dim addr = Dns.GetHostEntry(hostName)
            ipaddr = addr.AddressList.First(Function(ip) ip.AddressFamily = AddressFamily.InterNetwork)
        End Try
        Return ipaddr
    End Function

    ''' <summary>クライアント機能を開始します。</summary>
    ''' <param name="hostName">ホスト名。</param>
    ''' <param name="servicePort">サービスポート。</param>
    ''' <param name="timeoutLimit">サーバー接続タイムアウト時間。</param>
    ''' <param name="logger">ログ出力機能（指定がなければデフォルト出力）</param>
    ''' <returns>クライアント機能。</returns>
    Public Shared Function ClientRun(hostName As String,
                                     servicePort As Integer,
                                     Optional ByVal timeoutLimit As TimeSpan? = Nothing,
                                     Optional ByVal logger As ILogger = Nothing) As SimpleClient
        Try
            If logger Is Nothing Then
                logger = New MyLogger()
            End If
            Dim timeLimit = If(timeoutLimit, TimeSpan.FromSeconds(30))

            ' 送信先ホストのアドレスを取得します
            Dim ipaddr As IPAddress = ParseIPAddress(hostName)

            ' 受信用のポートを開く
            Dim listener = New TcpListener(ipaddr, 0)
            listener.Start()
            Dim recvPort = (CType(listener.LocalEndpoint, IPEndPoint)).Port
            logger?.LoggingInformation($"TCP/IP client start to server {hostName}({ipaddr}):{servicePort}")

            ' クライアント機能を生成
            Dim self As New SimpleClient(listener, logger)

            Using client As New TcpClient()
                client.Connect(IPAddress.Parse(ipaddr.ToString()), servicePort)
                Using stream = client.GetStream()
                    stream.WriteByte(COMMAND_HELLO)
                    WriteUInt16(stream, CUShort(recvPort))
                    stream.Flush()
                End Using

                ' 別スレッドでポートを監視
                self.mTask = Task.Factory.StartNew(
                    Sub()
                        self.ListenMethod(timeLimit)
                    End Sub,
                    TaskCreationOptions.LongRunning
                )
            End Using
            Return self

        Catch ex As Exception
            Throw New InvalidOperationException($"client connection error:{ex.Message}", ex)
        End Try
    End Function

    Private Sub ListenMethod(timeoutLimit As TimeSpan)
        Dim strtTm As Date = Date.Now
        Do While Date.Now.Subtract(strtTm) < timeoutLimit
            ' サーバーから接続要求があることを確認して接続し、応答情報を取得
            If Me.mListener.Pending() Then
                Dim revclient = Me.mListener.AcceptTcpClient()
                Dim stream = revclient.GetStream()

                ' 公開キーを取得
                Dim publicKey = ReadString(stream)

                ' 共通キーを作成
                Dim aesKey = RandomString(32)
                Dim aseIv = RandomString(16)
                Dim data = RasEncrypt($"{aesKey}:{aseIv}", publicKey)
                WriteInteger(stream, data.Length)
                stream.Write(data, 0, data.Length)
                stream.Flush()

                ' 送受信待機
                Using aesLapper = New AesLapper(Encoding.UTF8.GetBytes(aesKey), Encoding.UTF8.GetBytes(aseIv))
                    Do While True
                        ' 実行フラグがオフになればループ終了
                        SyncLock Me
                            If Not Me.mRunning Then
                                CommunicationExitWrite(stream)
                                GoTo EXIT_LOOP
                            End If
                        End SyncLock

                        ' リクエストを取得
                        Dim pair As (req As LocalComm, res As Communication) = Nothing
                        Dim dtty = DataType.NoneType
                        SyncLock Me
                            If Me.mRequests.Count > 0 Then
                                pair = Me.mRequests.Dequeue()
                                dtty = pair.req.ValueType
                            End If
                        End SyncLock

                        ' リクエストがあれば送信
                        Select Case dtty
                            Case DataType.StringType
                                CommunicationWrite(stream, aesLapper, pair.req.ValueString)
                                Me.GetResponse(stream, aesLapper, pair.res)

                            Case DataType.IntegerType
                                CommunicationWrite(stream, If(pair.req.ValueInteger, 0))
                                Me.GetResponse(stream, aesLapper, pair.res)

                            Case DataType.BytesType
                                CommunicationWrite(stream, aesLapper, pair.req.ValueBytes)
                                Me.GetResponse(stream, aesLapper, pair.res)

                            Case DataType.NoneType
                                Threading.Thread.Sleep(10)

                            Case Else
                                Me.mLogger?.LoggingError($"not target type:{pair.res.ValueType}")
                                Threading.Thread.Sleep(10)
                        End Select
                    Loop
                End Using
            Else
                Threading.Thread.Sleep(10)
            End If
        Loop
EXIT_LOOP:
    End Sub

    Public Function Write(input As String) As Communication
        Try
            Dim res As New Communication() With {.ValueType = DataType.NoneType}
            SyncLock Me
                Me.mRequests.Enqueue(
                    (New LocalComm() With {.ValueType = DataType.StringType, .ValueString = input}, res)
                )
            End SyncLock

            Do While True
                SyncLock Me
                    If res.ValueType <> DataType.NoneType Then Exit Do
                End SyncLock
                Threading.Thread.Sleep(1)
            Loop

            Return res
        Catch ex As Exception

        End Try
    End Function

    Public Function Write(input As Integer) As Communication
        Try
            Dim res As New Communication() With {.ValueType = DataType.NoneType}
            SyncLock Me
                Me.mRequests.Enqueue(
                    (New LocalComm() With {.ValueType = DataType.IntegerType, .ValueInteger = input}, res)
                )
            End SyncLock

            Do While True
                SyncLock Me
                    If res.ValueType <> DataType.NoneType Then Exit Do
                End SyncLock
                Threading.Thread.Sleep(1)
            Loop

            Return res
        Catch ex As Exception

        End Try
    End Function

    Public Function Write(input As Byte()) As Communication
        Try
            Dim res As New Communication() With {.ValueType = DataType.NoneType}
            SyncLock Me
                Me.mRequests.Enqueue(
                    (New LocalComm() With {.ValueType = DataType.BytesType, .ValueBytes = input}, res)
                )
            End SyncLock

            Do While True
                SyncLock Me
                    If res.ValueType <> DataType.NoneType Then Exit Do
                End SyncLock
                Threading.Thread.Sleep(1)
            Loop

            Return res
        Catch ex As Exception

        End Try
    End Function

    Private Sub GetResponse(stream As NetworkStream, aesLapper As AesLapper, res As Communication)
        Dim dataLen = ReadInteger(stream)
        If dataLen >= 0 Then
            Dim no = stream.ReadByte()
            Dim data As Byte()

            Select Case no
                Case DataType.IntegerType
                    data = ReadBytes(stream, dataLen - 1)
                    Dim inum = BitConverter.ToInt32(data, 0)
                    SyncLock Me
                        res.ValueType = DataType.IntegerType
                        res.ValueInteger = inum
                    End SyncLock

                Case DataType.StringType
                    data = aesLapper.Decrypt(ReadBytes(stream, dataLen - 1))
                    Dim message = Encoding.UTF8.GetString(data)
                    SyncLock Me
                        res.ValueType = DataType.StringType
                        res.ValueString = message
                    End SyncLock

                Case DataType.BytesType

            End Select
        End If
    End Sub

    Public NotInheritable Class LocalComm

        ''' <summary>データタイプを設定、取得します。</summary>
        Public Property ValueType As DataType = DataType.NoneType

        ''' <summary>整数値を設定、取得します。</summary>
        Public Property ValueInteger As Integer? = Nothing

        ''' <summary>文字列を設定、取得します。</summary>
        Public Property ValueString As String = Nothing

        ''' <summary>バイト配列を設定、取得します。</summary>
        Public Property ValueBytes As Byte() = Nothing

        ''' <summary>コンストラクタ。</summary>
        Public Sub New()

        End Sub

    End Class

End Class
