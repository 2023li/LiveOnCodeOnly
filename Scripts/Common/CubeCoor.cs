using UnityEngine;
using System;

/// <summary>
/// 六边形立方体坐标 (q + r + s = 0)
/// </summary>
[Serializable]
public struct CubeCoor : IEquatable<CubeCoor>
{
    public int q;
    public int r;
    public int s;

    public CubeCoor(int q, int r, int s)
    {
        this.q = q;
        this.r = r;
        this.s = s;
        if (q + r + s != 0) Debug.LogError($"CubeCoor 必须满足 q+r+s=0 -> {q},{r},{s}");
    }

    // 运算符重载
    public static CubeCoor operator +(CubeCoor a, CubeCoor b) => new CubeCoor(a.q + b.q, a.r + b.r, a.s + b.s);
    public static CubeCoor operator -(CubeCoor a, CubeCoor b) => new CubeCoor(a.q - b.q, a.r - b.r, a.s - b.s);
    public static CubeCoor operator *(CubeCoor a, int k) => new CubeCoor(a.q * k, a.r * k, a.s * k);
    public static bool operator ==(CubeCoor a, CubeCoor b) => a.q == b.q && a.r == b.r && a.s == b.s;
    public static bool operator !=(CubeCoor a, CubeCoor b) => !(a == b);

    public bool Equals(CubeCoor other) => this == other;
    public override bool Equals(object obj) => obj is CubeCoor other && this == other;
    public override int GetHashCode() => (q, r, s).GetHashCode();
    public override string ToString() => $"({q}, {r}, {s})";

    /// <summary>
    /// 到原点的距离
    /// </summary>
    public int Length() => (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;

    /// <summary>
    /// 到另一个点的距离
    /// </summary>
    public int DistanceTo(CubeCoor other) => (this - other).Length();

    // ---------------------------------------------------------
    //  新增：隐式转换到 Vector3Int
    // ---------------------------------------------------------
    /// <summary>
    /// 隐式转换为 Vector3Int (x=q, y=r, z=s)
    /// </summary>
    public static implicit operator Vector3Int(CubeCoor c)
    {
        return new Vector3Int(c.q, c.r, c.s);
    }
    // 如果你也想支持反向转换 (显式转换更安全，因为 Vector3Int 不一定满足 q+r+s=0)
    public static explicit operator CubeCoor(Vector3Int v)
    {
        return new CubeCoor(v.x, v.y, v.z);
    }
}
