// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

namespace PacketProtocol
{

using global::System;
using global::System.Collections.Generic;
using global::FlatBuffers;

public struct PacketMsg : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static void ValidateVersion() { FlatBufferConstants.FLATBUFFERS_1_12_0(); }
  public static PacketMsg GetRootAsPacketMsg(ByteBuffer _bb) { return GetRootAsPacketMsg(_bb, new PacketMsg()); }
  public static PacketMsg GetRootAsPacketMsg(ByteBuffer _bb, PacketMsg obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p = new Table(_i, _bb); }
  public PacketMsg __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  public PacketProtocol.sPacketInfo? PacketInfo { get { int o = __p.__offset(4); return o != 0 ? (PacketProtocol.sPacketInfo?)(new PacketProtocol.sPacketInfo()).__assign(o + __p.bb_pos, __p.bb) : null; } }
  public string Msg { get { int o = __p.__offset(6); return o != 0 ? __p.__string(o + __p.bb_pos) : null; } }
#if ENABLE_SPAN_T
  public Span<byte> GetMsgBytes() { return __p.__vector_as_span<byte>(6, 1); }
#else
  public ArraySegment<byte>? GetMsgBytes() { return __p.__vector_as_arraysegment(6); }
#endif
  public byte[] GetMsgArray() { return __p.__vector_as_array<byte>(6); }

  public static void StartPacketMsg(FlatBufferBuilder builder) { builder.StartTable(2); }
  public static void AddPacketInfo(FlatBufferBuilder builder, Offset<PacketProtocol.sPacketInfo> packetInfoOffset) { builder.AddStruct(0, packetInfoOffset.Value, 0); }
  public static void AddMsg(FlatBufferBuilder builder, StringOffset msgOffset) { builder.AddOffset(1, msgOffset.Value, 0); }
  public static Offset<PacketProtocol.PacketMsg> EndPacketMsg(FlatBufferBuilder builder) {
    int o = builder.EndTable();
    return new Offset<PacketProtocol.PacketMsg>(o);
  }
  public static void FinishPacketMsgBuffer(FlatBufferBuilder builder, Offset<PacketProtocol.PacketMsg> offset) { builder.Finish(offset.Value); }
  public static void FinishSizePrefixedPacketMsgBuffer(FlatBufferBuilder builder, Offset<PacketProtocol.PacketMsg> offset) { builder.FinishSizePrefixed(offset.Value); }
};


}
