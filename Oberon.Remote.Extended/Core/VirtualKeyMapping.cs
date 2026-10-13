namespace Oberon.Extended;

// User-editable virtual-key bindings. Values are Windows virtual-key codes,
// not scan codes, allowing support for arrows, function keys and modifiers.
public sealed record VirtualKeyMapping(
    int MoveUp = 0x57, int MoveDown = 0x53, int MoveLeft = 0x41, int MoveRight = 0x44,
    int LookUp = 0x26, int LookDown = 0x28, int LookLeft = 0x25, int LookRight = 0x27,
    int A = 0x4A, int B = 0x4B, int X = 0x55, int Y = 0x49,
    int LB = 0x51, int RB = 0x45, int Start = 0x0D, int Back = 0x08,
    int LStick = 0x10, int RStick = 0x11, int Guide = 0x09,
    int DpadUp = 0x38, int DpadDown = 0x35, int DpadLeft = 0x34, int DpadRight = 0x36,
    int LT = 0x01, int RT = 0x02)
{
    public static VirtualKeyMapping Default { get; } = new();

    public static readonly (string Id, string Label)[] Entries =
    [
        ("MoveUp","Analógico esquerdo: cima"),
        ("MoveDown","Analógico esquerdo: baixo"),
        ("MoveLeft","Analógico esquerdo: esquerda"),
        ("MoveRight","Analógico esquerdo: direita"),
        ("LookUp","Analógico direito: cima"),
        ("LookDown","Analógico direito: baixo"),
        ("LookLeft","Analógico direito: esquerda"),
        ("LookRight","Analógico direito: direita"),
        ("A","Botão A"),("B","Botão B"),("X","Botão X"),("Y","Botão Y"),
        ("LB","LB"),("RB","RB"),("LT","Gatilho LT"),("RT","Gatilho RT"),
        ("Start","Start / Menu"),("Back","View / Voltar"),
        ("LStick","Clique analógico esquerdo"),("RStick","Clique analógico direito"),
        ("Guide","Botão central"),("DpadUp","Direcional: cima"),
        ("DpadDown","Direcional: baixo"),("DpadLeft","Direcional: esquerda"),
        ("DpadRight","Direcional: direita")
    ];

    public int Get(string id) => id switch
    {
        nameof(MoveUp) => MoveUp, nameof(MoveDown) => MoveDown,
        nameof(MoveLeft) => MoveLeft, nameof(MoveRight) => MoveRight,
        nameof(LookUp) => LookUp, nameof(LookDown) => LookDown,
        nameof(LookLeft) => LookLeft, nameof(LookRight) => LookRight,
        nameof(A) => A, nameof(B) => B, nameof(X) => X, nameof(Y) => Y,
        nameof(LB) => LB, nameof(RB) => RB, nameof(LT) => LT, nameof(RT) => RT,
        nameof(Start) => Start, nameof(Back) => Back,
        nameof(LStick) => LStick, nameof(RStick) => RStick, nameof(Guide) => Guide,
        nameof(DpadUp) => DpadUp, nameof(DpadDown) => DpadDown,
        nameof(DpadLeft) => DpadLeft, nameof(DpadRight) => DpadRight,
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    public VirtualKeyMapping WithKey(string id, int key) => id switch
    {
        nameof(MoveUp) => this with { MoveUp=key },
        nameof(MoveDown) => this with { MoveDown=key },
        nameof(MoveLeft) => this with { MoveLeft=key },
        nameof(MoveRight) => this with { MoveRight=key },
        nameof(LookUp) => this with { LookUp=key },
        nameof(LookDown) => this with { LookDown=key },
        nameof(LookLeft) => this with { LookLeft=key },
        nameof(LookRight) => this with { LookRight=key },
        nameof(A) => this with { A=key }, nameof(B) => this with { B=key },
        nameof(X) => this with { X=key }, nameof(Y) => this with { Y=key },
        nameof(LB) => this with { LB=key }, nameof(RB) => this with { RB=key },
        nameof(LT) => this with { LT=key }, nameof(RT) => this with { RT=key },
        nameof(Start) => this with { Start=key }, nameof(Back) => this with { Back=key },
        nameof(LStick) => this with { LStick=key }, nameof(RStick) => this with { RStick=key },
        nameof(Guide) => this with { Guide=key },
        nameof(DpadUp) => this with { DpadUp=key },
        nameof(DpadDown) => this with { DpadDown=key },
        nameof(DpadLeft) => this with { DpadLeft=key },
        nameof(DpadRight) => this with { DpadRight=key },
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    public static bool IsAssignable(int key) =>
        key >= 1 && key <= 254 && key is not (0x77 or 0x5B or 0x5C or 0x12);
}
