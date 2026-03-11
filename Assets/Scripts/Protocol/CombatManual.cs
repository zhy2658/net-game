using System;
using System.IO;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Protocol {

    public sealed partial class SkillInfo : IMessage<SkillInfo> {
        private static readonly MessageParser<SkillInfo> _parser = new MessageParser<SkillInfo>(() => new SkillInfo());
        public static MessageParser<SkillInfo> Parser { get { return _parser; } }
        public static MessageDescriptor Descriptor { get { return null; } }
        MessageDescriptor IMessage.Descriptor { get { return null; } }

        public int SkillId { get; set; }
        public string TargetId { get; set; } = "";
        public Vector3 Direction { get; set; }
        public long Timestamp { get; set; }

        public SkillInfo() { OnConstruction(); }
        partial void OnConstruction();
        public SkillInfo(SkillInfo other) : this() {
            SkillId = other.SkillId;
            TargetId = other.TargetId;
            Direction = other.Direction != null ? other.Direction.Clone() : null;
            Timestamp = other.Timestamp;
        }

        public SkillInfo Clone() { return new SkillInfo(this); }

        public void WriteTo(CodedOutputStream output) {
            if (SkillId != 0) { output.WriteRawTag(8); output.WriteInt32(SkillId); }
            if (TargetId.Length != 0) { output.WriteRawTag(18); output.WriteString(TargetId); }
            if (Direction != null) { output.WriteRawTag(26); output.WriteMessage(Direction); }
            if (Timestamp != 0L) { output.WriteRawTag(32); output.WriteInt64(Timestamp); }
        }

        public int CalculateSize() {
            int size = 0;
            if (SkillId != 0) size += 1 + CodedOutputStream.ComputeInt32Size(SkillId);
            if (TargetId.Length != 0) size += 1 + CodedOutputStream.ComputeStringSize(TargetId);
            if (Direction != null) size += 1 + CodedOutputStream.ComputeMessageSize(Direction);
            if (Timestamp != 0L) size += 1 + CodedOutputStream.ComputeInt64Size(Timestamp);
            return size;
        }

        public void MergeFrom(CodedInputStream input) {
            uint tag;
            while ((tag = input.ReadTag()) != 0) {
                switch(tag) {
                    default: input.SkipLastField(); break;
                    case 8: SkillId = input.ReadInt32(); break;
                    case 18: TargetId = input.ReadString(); break;
                    case 26: 
                        if (Direction == null) Direction = new Vector3();
                        input.ReadMessage(Direction); 
                        break;
                    case 32: Timestamp = input.ReadInt64(); break;
                }
            }
        }
        public void MergeFrom(SkillInfo other) {
            if (other == null) return;
            if (other.SkillId != 0) SkillId = other.SkillId;
            if (other.TargetId.Length != 0) TargetId = other.TargetId;
            if (other.Direction != null) {
                if (Direction == null) Direction = new Vector3();
                Direction.MergeFrom(other.Direction);
            }
            if (other.Timestamp != 0L) Timestamp = other.Timestamp;
        }
        public bool Equals(SkillInfo other) {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(other, this)) return true;
            if (SkillId != other.SkillId) return false;
            if (TargetId != other.TargetId) return false;
            if (!object.Equals(Direction, other.Direction)) return false;
            if (Timestamp != other.Timestamp) return false;
            return true;
        }
        public override bool Equals(object other) { return Equals(other as SkillInfo); }
        public override int GetHashCode() { return SkillId.GetHashCode() ^ TargetId.GetHashCode() ^ (Direction?.GetHashCode() ?? 0) ^ Timestamp.GetHashCode(); }
    }

    public sealed partial class CastSkillRequest : IMessage<CastSkillRequest> {
        private static readonly MessageParser<CastSkillRequest> _parser = new MessageParser<CastSkillRequest>(() => new CastSkillRequest());
        public static MessageParser<CastSkillRequest> Parser { get { return _parser; } }
        public static MessageDescriptor Descriptor { get { return null; } }
        MessageDescriptor IMessage.Descriptor { get { return null; } }

        public SkillInfo SkillInfo { get; set; }

        public CastSkillRequest() {}
        public CastSkillRequest(CastSkillRequest other) : this() {
            SkillInfo = other.SkillInfo != null ? other.SkillInfo.Clone() : null;
        }
        public CastSkillRequest Clone() { return new CastSkillRequest(this); }
        public void WriteTo(CodedOutputStream output) {
            if (SkillInfo != null) { output.WriteRawTag(10); output.WriteMessage(SkillInfo); }
        }
        public int CalculateSize() {
            int size = 0;
            if (SkillInfo != null) size += 1 + CodedOutputStream.ComputeMessageSize(SkillInfo);
            return size;
        }
        public void MergeFrom(CodedInputStream input) {
            uint tag;
            while ((tag = input.ReadTag()) != 0) {
                switch(tag) {
                    default: input.SkipLastField(); break;
                    case 10: 
                        if (SkillInfo == null) SkillInfo = new SkillInfo();
                        input.ReadMessage(SkillInfo); 
                        break;
                }
            }
        }
        public void MergeFrom(CastSkillRequest other) {
            if (other == null) return;
            if (other.SkillInfo != null) {
                if (SkillInfo == null) SkillInfo = new SkillInfo();
                SkillInfo.MergeFrom(other.SkillInfo);
            }
        }
        public bool Equals(CastSkillRequest other) { return other != null && object.Equals(SkillInfo, other.SkillInfo); }
        public override bool Equals(object other) { return Equals(other as CastSkillRequest); }
        public override int GetHashCode() { return SkillInfo?.GetHashCode() ?? 0; }
    }

    public sealed partial class SkillCastPush : IMessage<SkillCastPush> {
        private static readonly MessageParser<SkillCastPush> _parser = new MessageParser<SkillCastPush>(() => new SkillCastPush());
        public static MessageParser<SkillCastPush> Parser { get { return _parser; } }
        public static MessageDescriptor Descriptor { get { return null; } }
        MessageDescriptor IMessage.Descriptor { get { return null; } }

        public string CasterId { get; set; } = "";
        public SkillInfo SkillInfo { get; set; }

        public SkillCastPush() {}
        public SkillCastPush(SkillCastPush other) : this() {
            CasterId = other.CasterId;
            SkillInfo = other.SkillInfo != null ? other.SkillInfo.Clone() : null;
        }
        public SkillCastPush Clone() { return new SkillCastPush(this); }
        public void WriteTo(CodedOutputStream output) {
            if (CasterId.Length != 0) { output.WriteRawTag(10); output.WriteString(CasterId); }
            if (SkillInfo != null) { output.WriteRawTag(18); output.WriteMessage(SkillInfo); }
        }
        public int CalculateSize() {
            int size = 0;
            if (CasterId.Length != 0) size += 1 + CodedOutputStream.ComputeStringSize(CasterId);
            if (SkillInfo != null) size += 1 + CodedOutputStream.ComputeMessageSize(SkillInfo);
            return size;
        }
        public void MergeFrom(CodedInputStream input) {
            uint tag;
            while ((tag = input.ReadTag()) != 0) {
                switch(tag) {
                    default: input.SkipLastField(); break;
                    case 10: CasterId = input.ReadString(); break;
                    case 18: 
                        if (SkillInfo == null) SkillInfo = new SkillInfo();
                        input.ReadMessage(SkillInfo); 
                        break;
                }
            }
        }
        public void MergeFrom(SkillCastPush other) {
            if (other == null) return;
            if (other.CasterId.Length != 0) CasterId = other.CasterId;
            if (other.SkillInfo != null) {
                if (SkillInfo == null) SkillInfo = new SkillInfo();
                SkillInfo.MergeFrom(other.SkillInfo);
            }
        }
        public bool Equals(SkillCastPush other) {
            if (other == null) return false;
            if (CasterId != other.CasterId) return false;
            if (!object.Equals(SkillInfo, other.SkillInfo)) return false;
            return true;
        }
        public override bool Equals(object other) { return Equals(other as SkillCastPush); }
        public override int GetHashCode() { return CasterId.GetHashCode() ^ (SkillInfo?.GetHashCode() ?? 0); }
    }

    public sealed partial class AttributeUpdatePush : IMessage<AttributeUpdatePush> {
        private static readonly MessageParser<AttributeUpdatePush> _parser = new MessageParser<AttributeUpdatePush>(() => new AttributeUpdatePush());
        public static MessageParser<AttributeUpdatePush> Parser { get { return _parser; } }
        public static MessageDescriptor Descriptor { get { return null; } }
        MessageDescriptor IMessage.Descriptor { get { return null; } }

        public string TargetId { get; set; } = "";
        public int Damage { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public bool IsDead { get; set; }

        public AttributeUpdatePush() {}
        public AttributeUpdatePush(AttributeUpdatePush other) : this() {
            TargetId = other.TargetId;
            Damage = other.Damage;
            CurrentHp = other.CurrentHp;
            MaxHp = other.MaxHp;
            IsDead = other.IsDead;
        }
        public AttributeUpdatePush Clone() { return new AttributeUpdatePush(this); }
        public void WriteTo(CodedOutputStream output) {
            if (TargetId.Length != 0) { output.WriteRawTag(10); output.WriteString(TargetId); }
            if (Damage != 0) { output.WriteRawTag(16); output.WriteInt32(Damage); }
            if (CurrentHp != 0) { output.WriteRawTag(24); output.WriteInt32(CurrentHp); }
            if (MaxHp != 0) { output.WriteRawTag(32); output.WriteInt32(MaxHp); }
            if (IsDead) { output.WriteRawTag(40); output.WriteBool(IsDead); }
        }
        public int CalculateSize() {
            int size = 0;
            if (TargetId.Length != 0) size += 1 + CodedOutputStream.ComputeStringSize(TargetId);
            if (Damage != 0) size += 1 + CodedOutputStream.ComputeInt32Size(Damage);
            if (CurrentHp != 0) size += 1 + CodedOutputStream.ComputeInt32Size(CurrentHp);
            if (MaxHp != 0) size += 1 + CodedOutputStream.ComputeInt32Size(MaxHp);
            if (IsDead) size += 1 + 1;
            return size;
        }
        public void MergeFrom(CodedInputStream input) {
            uint tag;
            while ((tag = input.ReadTag()) != 0) {
                switch(tag) {
                    default: input.SkipLastField(); break;
                    case 10: TargetId = input.ReadString(); break;
                    case 16: Damage = input.ReadInt32(); break;
                    case 24: CurrentHp = input.ReadInt32(); break;
                    case 32: MaxHp = input.ReadInt32(); break;
                    case 40: IsDead = input.ReadBool(); break;
                }
            }
        }
        public void MergeFrom(AttributeUpdatePush other) {
            if (other == null) return;
            if (other.TargetId.Length != 0) TargetId = other.TargetId;
            if (other.Damage != 0) Damage = other.Damage;
            if (other.CurrentHp != 0) CurrentHp = other.CurrentHp;
            if (other.MaxHp != 0) MaxHp = other.MaxHp;
            if (other.IsDead) IsDead = other.IsDead;
        }
        public bool Equals(AttributeUpdatePush other) {
            if (other == null) return false;
            if (TargetId != other.TargetId) return false;
            if (Damage != other.Damage) return false;
            if (CurrentHp != other.CurrentHp) return false;
            if (MaxHp != other.MaxHp) return false;
            if (IsDead != other.IsDead) return false;
            return true;
        }
        public override bool Equals(object other) { return Equals(other as AttributeUpdatePush); }
        public override int GetHashCode() { return TargetId.GetHashCode() ^ Damage.GetHashCode(); }
    }

    public sealed partial class PlayerDeadPush : IMessage<PlayerDeadPush> {
        private static readonly MessageParser<PlayerDeadPush> _parser = new MessageParser<PlayerDeadPush>(() => new PlayerDeadPush());
        public static MessageParser<PlayerDeadPush> Parser { get { return _parser; } }
        public static MessageDescriptor Descriptor { get { return null; } }
        MessageDescriptor IMessage.Descriptor { get { return null; } }

        public string Id { get; set; } = "";
        public string KillerId { get; set; } = "";

        public PlayerDeadPush() {}
        public PlayerDeadPush(PlayerDeadPush other) : this() {
            Id = other.Id;
            KillerId = other.KillerId;
        }
        public PlayerDeadPush Clone() { return new PlayerDeadPush(this); }
        public void WriteTo(CodedOutputStream output) {
            if (Id.Length != 0) { output.WriteRawTag(10); output.WriteString(Id); }
            if (KillerId.Length != 0) { output.WriteRawTag(18); output.WriteString(KillerId); }
        }
        public int CalculateSize() {
            int size = 0;
            if (Id.Length != 0) size += 1 + CodedOutputStream.ComputeStringSize(Id);
            if (KillerId.Length != 0) size += 1 + CodedOutputStream.ComputeStringSize(KillerId);
            return size;
        }
        public void MergeFrom(CodedInputStream input) {
            uint tag;
            while ((tag = input.ReadTag()) != 0) {
                switch(tag) {
                    default: input.SkipLastField(); break;
                    case 10: Id = input.ReadString(); break;
                    case 18: KillerId = input.ReadString(); break;
                }
            }
        }
        public void MergeFrom(PlayerDeadPush other) {
            if (other == null) return;
            if (other.Id.Length != 0) Id = other.Id;
            if (other.KillerId.Length != 0) KillerId = other.KillerId;
        }
        public bool Equals(PlayerDeadPush other) {
            if (other == null) return false;
            if (Id != other.Id) return false;
            if (KillerId != other.KillerId) return false;
            return true;
        }
        public override bool Equals(object other) { return Equals(other as PlayerDeadPush); }
        public override int GetHashCode() { return Id.GetHashCode() ^ KillerId.GetHashCode(); }
    }
}
