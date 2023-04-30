Option Strict On
Option Explicit On

''' <summary>データタイプ値です。</summary>
Public Enum DataType As Byte

    ''' <summary>未指定です。</summary>
    NoneType = &H20

    ''' <summary>終了です。</summary>
    ExitType = &H21

    ''' <summary>整数タイプです。</summary>
    IntegerType = &H22

    ''' <summary>文字列タイプです。</summary>
    StringType = &H23

    ''' <summary>バイト配列タイプです。</summary>
    BytesType = &H24

End Enum
