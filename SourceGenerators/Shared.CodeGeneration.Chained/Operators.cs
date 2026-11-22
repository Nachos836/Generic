namespace Shared.CodeGeneration.Chained;

internal enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    LeftShift,
    RightShift,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Equal,
    NotEqual,
    BitwiseAnd,
    BitwiseOr,
    ExclusiveOr,
    And,
    Or
}

internal enum Variance
{
    None,
    In,
    Out
}