public enum EnemyType
{
    /// <summary>パリィ多用。ゲージ最大8でガードが固い。</summary>
    ParrySpammer,

    /// <summary>攻撃特化。ガードゲージ最大4で防御は薄い。</summary>
    Attacker,

    /// <summary>フェイント多用。行動間隔0.5倍で素早い。</summary>
    Feinter,

    /// <summary>攻撃重視の速攻型。行動間隔0.5倍。</summary>
    Rusher,

    /// <summary>均等バランス型。行動間隔2倍で慎重。</summary>
    Careful,

    /// <summary>後退回避型。攻撃を見たら下がってよけ、隙を見て反撃する。</summary>
    Dodger,
}
