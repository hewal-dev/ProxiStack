using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProxiStack
{
    /// <summary>
    /// Netzwerkpaket für ProxiStack (Stash & Restock) im Multiplayer.
    /// Client → Server: Sendet Spieler-ID und Aktionstyp
    /// Server → Client: Sendet Container-Offsets zurück
    /// 
    /// Für 7DTD v3.0: NetPackage hat keine abstrakte SendTo() mehr.
    /// Stattdessen wird via ClientInfo.SendPackage() versendet.
    /// </summary>
    public class NetPackageProxiStack : NetPackage
    {
        private int playerEntityId;
        private ProxiStackType actionType;
        private List<Vector3i> containerOffsets;

        public NetPackageProxiStack Setup(int _playerEntityId, ProxiStackType _actionType)
        {
            playerEntityId = _playerEntityId;
            actionType = _actionType;
            containerOffsets = null;
            return this;
        }

        public NetPackageProxiStack Setup(int _playerEntityId, ProxiStackType _actionType, List<Vector3i> _offsets)
        {
            playerEntityId = _playerEntityId;
            actionType = _actionType;
            containerOffsets = _offsets;
            return this;
        }

        public override NetPackageDirection PackageDirection => NetPackageDirection.Both;
        public override bool AllowedBeforeAuth => false;

        // v3.3: NetPackage.GetLength() wurde entfernt (kein Vanilla-Paket überschreibt
        // es in V 3.3.0 b18 mehr). Der frühere Override verursachte
        // CS0115 "marked as an override but no suitable method found to override"
        // und damit einen Totalausfall des Mod-Ladens. Die Längenermittlung
        // übernimmt jetzt die Basisklasse.

        public override void read(PooledBinaryReader _reader)
        {
            try
            {
                playerEntityId = _reader.ReadInt32();
                actionType = (ProxiStackType)_reader.ReadByte();
                int count = _reader.ReadUInt16();
                containerOffsets = new List<Vector3i>(count);
                for (int i = 0; i < count; i++)
                {
                    containerOffsets.Add(new Vector3i(
                        _reader.ReadInt16(),
                        _reader.ReadInt16(),
                        _reader.ReadInt16()
                     ));
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] NetPackageProxiStack read error: {ex.Message}");
            }
        }

        public override void write(PooledBinaryWriter _writer)
        {
            try
            {
                base.write(_writer);
                _writer.Write(playerEntityId);
                _writer.Write((byte)actionType);
                int count = containerOffsets?.Count ?? 0;
                _writer.Write((ushort)count);
                if (containerOffsets != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        _writer.Write((short)containerOffsets[i].x);
                        _writer.Write((short)containerOffsets[i].y);
                        _writer.Write((short)containerOffsets[i].z);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] NetPackageProxiStack write error: {ex.Message}");
            }
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            try
            {
                // Server erhält Anfrage von Client
                if (ConnectionManager.Instance.IsServer)
                {
                    var player = _world.GetEntity(playerEntityId) as EntityPlayer;
                    if (player == null) return;

                    // Container im Radius finden
                    var config = ProxiStackMod.Instance.Config;
                    var center = new Vector3i(player.position);
                    var offsets = new List<Vector3i>();

                    var containers = ContainerHelper.FindNearbyContainersStash(center, config.StashDistance);
                    foreach (var result in containers)
                    {
                        if (result.TileEntity != null)
                        {
                            Vector3i pos = result.TileEntity.ToWorldPos();
                            offsets.Add(pos - center);
                        }
                    }

                    // Antwort an Client senden
                    var response = NetPackageManager.GetPackage<NetPackageProxiStack>()
                        .Setup(playerEntityId, actionType, offsets);

                    var clientInfo = ConnectionManager.Instance.Clients.ForEntityId(playerEntityId);
                    if (clientInfo != null)
                    {
                        clientInfo.SendPackage(response);
                    }
                }
                else
                {
                    // Client erhält Antwort vom Server
                    var player = _world.GetEntity(playerEntityId) as EntityPlayer;
                    if (player == null) return;

                    Vector3i center = new Vector3i(player.position);

                    if (containerOffsets != null)
                    {
                        switch (actionType)
                        {
                            case ProxiStackType.Stack:
                                ProxiStackManager.PerformStashClient(center, containerOffsets);
                                break;
                            case ProxiStackType.Restock:
                                ProxiStackManager.PerformRestockClient(center, containerOffsets);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] NetPackageProxiStack process error: {ex.Message}");
            }
        }
    }
}