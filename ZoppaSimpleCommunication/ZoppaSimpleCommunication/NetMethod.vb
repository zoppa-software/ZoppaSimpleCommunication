Option Strict On
Option Explicit On
Imports System.Net.Sockets
Imports System.Text

''' <summary>通信メソッドを実装します。</summary>
Public Module NetMethod

    ''' <summary>通信開始コード。</summary>
    Public Const COMMAND_HELLO As Byte = &H10

    ''' <summary>バッファサイズ。</summary>
    Public Const READ_BUF_SIZE As Integer = 4096 - 1

    ''' <summary>ランダムな文字列を生成します。</summary>
    ''' <param name="count">文字数。</param>
    ''' <returns>ランダム文字列。</returns>
    Public Function RandomString(count As Integer) As String
        Dim chTable = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray()
        Dim rnd As New Random()

        Dim res As New StringBuilder()
        For i As Integer = 0 To count - 1
            res.Append(chTable(rnd.Next(chTable.Length)))
        Next

        Return res.ToString()
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <returns></returns>
    Friend Function RsaCreateKeys() As (publicKey As String, privateKey As String)
        Dim rsa As New System.Security.Cryptography.RSACryptoServiceProvider()
        Return (rsa.ToXmlString(False), rsa.ToXmlString(True))
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="str"></param>
    ''' <param name="publicKey"></param>
    ''' <returns></returns>
    Friend Function RasEncrypt(ByVal str As String, ByVal publicKey As String) As Byte()
        Using rsa As New System.Security.Cryptography.RSACryptoServiceProvider()
            '公開鍵を指定
            rsa.FromXmlString(publicKey)

            ' 暗号化する
            Return rsa.Encrypt(Encoding.UTF8.GetBytes(str), True)
        End Using
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="data"></param>
    ''' <param name="privateKey"></param>
    ''' <returns></returns>
    Friend Function RsaDecrypt(ByVal data As Byte(), ByVal privateKey As String) As String
        Using rsa As New System.Security.Cryptography.RSACryptoServiceProvider()
            '秘密鍵を指定
            rsa.FromXmlString(privateKey)

            '復号化する
            Dim decryptedData As Byte() = rsa.Decrypt(data, True)
            Return Encoding.UTF8.GetString(decryptedData)
        End Using
    End Function

    Friend Function ReadBytes(targetStream As IO.Stream, readCount As Integer) As Byte()
        Dim buf = New Byte(READ_BUF_SIZE) {}
        Dim res As New List(Of Byte)(READ_BUF_SIZE)

        Do While readCount > 0
            Dim cnt = targetStream.Read(buf, 0, If(readCount < buf.Length, readCount, buf.Length))
            If cnt > 0 Then
                res.AddRange(buf.Take(cnt))
                readCount -= cnt
            Else
                Threading.Thread.Sleep(10)
            End If
        Loop

        Return res.ToArray()
    End Function

    ''' <summary>ストリームへ数値を書き込みます。</summary>
    ''' <param name="targetStream">対象ストリーム。</param>
    ''' <param name="value">書き込む値。</param>
    Friend Sub WriteUInt16(targetStream As IO.Stream, value As UShort)
        With targetStream
            Dim num = BitConverter.GetBytes(value)
            .Write(num, 0, num.Length)
        End With
    End Sub

    ''' <summary>ネットワークストリームから数値を取得します。</summary>
    ''' <param name="targetStream">対象ストリーム。</param>
    ''' <returns>数値。</returns>
    Friend Function ReadInteger(targetStream As IO.Stream) As Integer
        Dim numbuf = New Byte(3) {}
        targetStream.Read(numbuf, 0, numbuf.Length)

        Return BitConverter.ToInt32(numbuf, 0)
    End Function

    ''' <summary>ストリームへ数値を書き込みます。</summary>
    ''' <param name="targetStream">対象ストリーム。</param>
    ''' <param name="value">書き込む値。</param>
    Friend Sub WriteInteger(ByVal targetStream As IO.Stream, ByVal value As Integer)
        With targetStream
            Dim num = BitConverter.GetBytes(value)
            .Write(num, 0, num.Length)
        End With
    End Sub

    ''' <summary>ネットワークストリームから数値を取得します。</summary>
    ''' <param name="targetStream">対象ストリーム。</param>
    ''' <returns>数値。</returns>
    Friend Function ReadUInt16(targetStream As IO.Stream) As UShort
        Dim numbuf = New Byte(1) {}
        targetStream.Read(numbuf, 0, numbuf.Length)

        Return BitConverter.ToUInt16(numbuf, 0)
    End Function

    ''' <summary>ストリームへ文字列を書き込むます。</summary>
    ''' <param name="targetStream">対象ストリーム。</param>
    ''' <param name="value">書き込む文字列。</param>
    Friend Sub WriteString(targetStream As IO.Stream, ByVal value As String)
        ' 文字列をUTF8のバイト配列に変換
        Dim chs = System.Text.Encoding.UTF8.GetBytes(value)

        ' バイト数を書き込み、続けてバイト配列を書き込む
        With targetStream
            Dim count = BitConverter.GetBytes(CUShort(chs.Length))
            .Write(count, 0, count.Length)
            .Write(chs, 0, chs.Length)
        End With
    End Sub

    ''' <summary>ストリームから文字列を読み込みます。</summary>
    ''' <param name="targetStream">対象ストリーム。</param>
    ''' <returns>読み込んだ文字列。</returns>
    Friend Function ReadString(targetStream As IO.Stream) As String
        ' 文字列のバイト数を取得
        Dim numbuf = New Byte(1) {}
        targetStream.Read(numbuf, 0, numbuf.Length)

        ' バイト数分の領域を取得して文字列を読み込む
        Dim len = BitConverter.ToUInt16(numbuf, 0)
        If len > 0 Then
            Dim bytes = New Byte(len - 1) {}
            targetStream.Read(bytes, 0, bytes.Length)

            Return System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length)
        Else
            Return ""
        End If
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="stream"></param>
    ''' <param name="aesLapper"></param>
    ''' <param name="input"></param>
    Friend Sub CommunicationWrite(stream As NetworkStream, aesLapper As AesLapper, input As String)
        Dim data = aesLapper.Encrypt(Encoding.UTF8.GetBytes(input))
        WriteInteger(stream, data.Length + 1)

        stream.WriteByte(DataType.StringType)

        stream.Write(data, 0, data.Length)
        stream.Flush()
    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="stream"></param>
    ''' <param name="input"></param>
    Friend Sub CommunicationExitWrite(stream As NetworkStream)
        WriteInteger(stream, 1)

        stream.WriteByte(DataType.ExitType)
        stream.Flush()
    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="stream"></param>
    ''' <param name="input"></param>
    Friend Sub CommunicationWrite(stream As NetworkStream, input As Integer)
        WriteInteger(stream, 4 + 1)

        stream.WriteByte(DataType.IntegerType)

        WriteInteger(stream, input)
        stream.Flush()
    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="stream"></param>
    ''' <param name="aesLapper"></param>
    ''' <param name=""></param>
    ''' <param name="input"></param>
    Friend Sub CommunicationWrite(stream As NetworkStream, aesLapper As AesLapper, input As Byte())
        Dim data = aesLapper.Encrypt(input)

        WriteInteger(stream, data.Length + 1)

        stream.WriteByte(DataType.BytesType)
        stream.Write(data, 0, data.Length)
        stream.Flush()
    End Sub

End Module
