Imports System.IO
Imports System.Security.Cryptography

Public NotInheritable Class AesLapper
    Implements IDisposable

    ' AES暗号化機能
    Private ReadOnly mAes As Aes

    Private ReadOnly mEncryptor As ICryptoTransform

    Private ReadOnly mDecryptor As ICryptoTransform

    Public Sub New(aesKey As Byte(), aesIv As Byte())
        Me.mAes = Aes.Create("AES")
        Me.mEncryptor = Me.mAes.CreateEncryptor(aesKey, aesIv)
        Me.mDecryptor = Me.mAes.CreateDecryptor(aesKey, aesIv)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Me.mEncryptor.Dispose()
        Me.mDecryptor.Dispose()
        Me.mAes.Dispose()
    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="input"></param>
    ''' <returns></returns>
    Friend Function Encrypt(input As Byte()) As Byte()
        Using mem As New MemoryStream()
            Using cs As New CryptoStream(mem, Me.mEncryptor, CryptoStreamMode.Write)
                cs.Write(input, 0, input.Length)
            End Using
            Return mem.ToArray()
        End Using
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="data"></param>
    ''' <returns></returns>
    Friend Function Decrypt(data As Byte()) As Byte()
        Using mem As New MemoryStream(data)
            Using cs As New CryptoStream(mem, Me.mDecryptor, CryptoStreamMode.Read)
                Dim res As New List(Of Byte)()
                Do While cs.CanRead
                    Dim buf = New Byte(READ_BUF_SIZE) {}
                    Dim readSize = cs.Read(buf, 0, buf.Length)
                    If readSize > 0 Then
                        res.AddRange(buf.Take(readSize))
                    Else
                        Exit Do
                    End If
                Loop
                Return res.ToArray()
            End Using
        End Using
    End Function

End Class
