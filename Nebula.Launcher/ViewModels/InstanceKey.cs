using System;

namespace Nebula.Launcher.ViewModels;

public record struct InstanceKey(int Id):
    IEquatable<int>,
    IComparable<InstanceKey>
{
    public static implicit operator InstanceKey(int id) => new InstanceKey(id);
    public static implicit operator int(InstanceKey id) => id.Id;
    public bool Equals(int other) => Id == other;
    public int CompareTo(InstanceKey other) => Id.CompareTo(other.Id);
};