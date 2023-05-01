Option Strict On
Option Explicit On

''' <summary>コミュニケーション情報です。</summary>
Public NotInheritable Class Communication

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

    ''' <summary>エラーコンストラクタ。</summary>
    ''' <param name="vType">データタイプ。</param>
    ''' <param name="errMsg">エラーメッセージ。</param>
    Public Sub New(vType As DataType, errMsg As String)
        Me.ValueType = vType
        Me.ValueString = errMsg
    End Sub

End Class