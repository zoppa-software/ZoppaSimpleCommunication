Option Strict On
Option Explicit On

Imports System.Net
Imports System.Net.Sockets
Imports System.Text

''' <summary>�V���v����TPC/IP�T�[�o�[�@�\�ł��B</summary>
Public NotInheritable Class SimpleServer
    Implements IDisposable

    ''' <summary>�N���C�A���g���烁�b�Z�[�W��M���������܂��B</summary>
    ''' <param name="request">��M���b�Z�[�W�B</param>
    ''' <param name="response">�ԐM���b�Z�[�W�B</param>
    Public Event ReceivedMessage(request As Communication, response As Communication)

    ' TCP���X�i�[
    Private ReadOnly mListener As TcpListener

    ' ���O�o�͋@�\
    Private ReadOnly mLogger As ILogger

    ' �N���C�A���g�^�X�N
    Private ReadOnly mClientTasks As New HashSet(Of TcpClient)()

    ' ���s���t���O
    Private mRunning As Boolean

    ' ���s�^�X�N
    Private mTask As Task

    ' �j���ς݃t���O
    Private mDisposed As Boolean

    ''' <summary>�R���X�g���N�^�B</summary>
    ''' <param name="listner">TCP���X�i�[�B</param>
    ''' <param name="logger">���O�o�͋@�\�B</param>
    Private Sub New(listner As TcpListener, logger As ILogger)
        Me.mListener = listner
        Me.mLogger = logger
        Me.mRunning = True
        Me.mDisposed = False
    End Sub

    ''' <summary>�T�[�o�[�@�\���J�n���܂��B</summary>
    ''' <param name="servicePort">�T�[�r�X�|�[�g�B</param>
    ''' <param name="logger">���O�o�͋@�\�i�w�肪�Ȃ���΃f�t�H���g�o�́j</param>
    ''' <returns>�T�[�o�[�@�\�B</returns>
    Public Shared Function ServerRun(servicePort As Integer,
                                     Optional ByVal logger As ILogger = Nothing) As SimpleServer
        ' ���O�o�͋@�\�̏�����
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

            ' �ʃX���b�h�Ń|�[�g���Ď�
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

    ''' <summary>���\�[�X�̉�����s���B</summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Me.mLogger?.LoggingInformation("Dispposing server")
        Me.StopServer()
    End Sub

    ''' <summary>�T�[�o�[�@�\���~���܂��B</summary>
    Public Sub StopServer()
        If Not Me.mDisposed Then
            SyncLock Me
                Me.mRunning = False
            End SyncLock

            Try
                ' �N���C�A���g�^�X�N���~
                Me.mLogger?.LoggingInformation("Stop client tasks")
                SyncLock Me
                    For Each client In Me.mClientTasks
                        client.Close()
                    Next
                    Me.mClientTasks.Clear()
                End SyncLock

                ' ���X�i�[���~
                Me.mLogger?.LoggingInformation("Stop listner")
                Me.mListener.Stop()

                ' �^�X�N���~
                Me.mLogger?.LoggingInformation("Stop main task")
                Me.mTask.Wait()
                Me.mTask.Dispose()

                Me.mDisposed = True

            Catch ex As Exception
                Me.mLogger?.LoggingError($"Failed to stop server function:{ex.Message}")
            End Try
        End If
    End Sub

    ''' <summary>TCP/IP�|�[�g�̊Ď������s���܂��B</summary>
    Private Sub ListenMethod()
        Do While True
            ' ���s�t���O���I�t�ɂȂ�΃��[�v�I��
            SyncLock Me
                If Not Me.mRunning Then
                    Exit Do
                End If
            End SyncLock

            Try
                ' -----------------------------------------
                ' �N���C�A���g���烁�b�Z�[�W����M���܂�
                ' -----------------------------------------
                Dim useAddress As IPAddress
                Dim useCommand As Integer = 0
                Dim usePort As UShort = 0

                Using client = Me.mListener.AcceptTcpClient()
                    ' �N���C�A���g�� IP�A�h���X���擾
                    useAddress = CType(client.Client.RemoteEndPoint, IPEndPoint).Address

                    Using stream = client.GetStream()
                        ' �R�}���h���擾
                        useCommand = stream.ReadByte()

                        ' �R�}���h���Ƃɏ�������
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
                ' �����X���b�h���J�n���܂�
                ' ---------------------------
                Dim resTask = Task.Run(
                    Sub()
                        Me.ClientTaskThread(useAddress, usePort)
                    End Sub
                )

            Catch ex As SocketException
                If ex.ErrorCode <> 10004 Then
                    Me.mLogger?.LoggingError($"�\�P�b�g�G���[:{ex.Message} �� �T�[�r�X�I�����ɂ��o�͂���邽�ߒ���")
                    Me.mLogger?.LoggingError(ex)
                End If

            Catch ex As Exception
                Me.mLogger?.LoggingError($"TCP/IP �T�[�o�[�G���[�F{ex.Message}")
                Me.mLogger?.LoggingError(ex)
            End Try

            Threading.Thread.Sleep(10)
        Loop
    End Sub

    ''' <summary>�N���C�A���g�ʐM�����s���܂��B</summary>
    ''' <param name="useAddress">�N���C�A���g�̃A�h���X�B</param>
    ''' <param name="usePort">�N���C�A���g�̃|�[�g�B</param>
    Private Sub ClientTaskThread(useAddress As IPAddress, usePort As UShort)
        Me.mLogger?.LoggingInformation($"Start communication by thread id:{Threading.Thread.CurrentThread.ManagedThreadId}")

        ' ���J�A�閧�����쐬
        Dim keys = RsaCreateKeys()
        Me.mLogger?.LoggingInformation($"Create RSA keys:{keys.publicKey}")

        Try
            Using client As New TcpClient(useAddress.ToString(), usePort)
                SyncLock Me
                    Me.mClientTasks.Add(client)
                End SyncLock

                Using stream = client.GetStream()
                    ' ���J�L�[�𑗐M
                    Me.mLogger?.LoggingInformation($"Send public key to {useAddress}:{usePort}")
                    WriteString(stream, keys.publicKey)
                    stream.Flush()

                    ' ���ʌ�����M
                    Dim commonKeyLen = ReadInteger(stream)
                    Dim commonKeyData = ReadBytes(stream, commonKeyLen)
                    Dim commonKeys = RsaDecrypt(commonKeyData, keys.privateKey).Split(":"c)
                    Dim aseKey = Encoding.UTF8.GetBytes(commonKeys(0))
                    Dim aseIv = Encoding.UTF8.GetBytes(commonKeys(1))
                    Me.mLogger?.LoggingInformation($"receive common key from {useAddress}:{usePort}")

                    ' ���b�Z�[�W�ҋ@
                    Using aesLapper As New AesLapper(aseKey, aseIv)
                        Do While True
                            Dim dataLen = ReadInteger(stream)
                            If dataLen >= 0 Then
                                ' -----------------------
                                ' ��M���b�Z�[�W������
                                ' -----------------------
                                Dim no = stream.ReadByte()
                                Dim data As Byte()

                                Dim request As New Communication()
                                Dim response As New Communication() With {.ValueType = DataType.IntegerType}
                                Select Case no
                                    Case DataType.ExitType
                                        ' �I���R�}���h����M
                                        Exit Do

                                    Case DataType.IntegerType
                                        ' �����l����M
                                        data = ReadBytes(stream, dataLen - 1)
                                        Dim inum = BitConverter.ToInt32(data, 0)
                                        Me.mLogger?.LoggingDebug($"receive from {useAddress}:{usePort} length:{dataLen} integer:{inum}")

                                        request.ValueType = DataType.IntegerType
                                        request.ValueInteger = inum
                                        RaiseEvent ReceivedMessage(request, response)

                                    Case DataType.StringType
                                        ' ���������M
                                        data = aesLapper.Decrypt(ReadBytes(stream, dataLen - 1))
                                        Dim message = Encoding.UTF8.GetString(data)
                                        Me.mLogger?.LoggingDebug($"receive from {useAddress}:{usePort} length:{dataLen} string:{If(message.Length < 25, message, message.Substring(0, 25) & "...")}")

                                        request.ValueType = DataType.StringType
                                        request.ValueString = message
                                        RaiseEvent ReceivedMessage(request, response)

                                    Case DataType.BytesType
                                        ' �o�C�g�z�����M
                                        data = aesLapper.Decrypt(ReadBytes(stream, dataLen - 1))
                                        Me.mLogger?.LoggingDebug($"receive from {useAddress}:{usePort} length:{dataLen} bytes:{GeBytesString(data)}")

                                        request.ValueType = DataType.BytesType
                                        request.ValueBytes = data
                                        RaiseEvent ReceivedMessage(request, response)
                                End Select

                                ' --------------------
                                ' ���b�Z�[�W��ԐM
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
            Me.mLogger?.LoggingError($"TCP/IP �T�[�o�[�G���[�F{ex.Message} thread id:{Threading.Thread.CurrentThread.ManagedThreadId}")
            Me.mLogger?.LoggingError(ex)
        End Try
    End Sub

    ''' <summary>�o�C�g�z������O�ɏo�͂��镶������擾���܂��B</summary>
    ''' <param name="datas">�o�C�g�z��B</param>
    ''' <returns>���O������B</returns>
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
