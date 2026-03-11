using UnityEngine;
using Protocol;

public class CombatManager : MonoBehaviour
{
    private NanoKcpClient _client;

    void Start()
    {
        _client = FindFirstObjectByType<NanoKcpClient>();
        if (_client != null)
        {
            _client.RegisterHandler("OnSkillCast", OnSkillCast);
            _client.RegisterHandler("OnAttributeUpdate", OnAttributeUpdate);
            _client.RegisterHandler("OnPlayerDead", OnPlayerDead);
            Debug.Log("[Combat] CombatManager initialized");
        }
        else
        {
            Debug.LogError("[Combat] NanoKcpClient not found!");
        }
    }

    private void OnSkillCast(byte[] data)
    {
        try {
            var msg = SkillCastPush.Parser.ParseFrom(data);
            Debug.Log($"[Combat] {msg.CasterId} cast skill {msg.SkillInfo?.SkillId ?? 0}");
            // TODO: Find player entity and play animation/effect
        } catch (System.Exception e) { Debug.LogError($"[Combat] Parse error: {e}"); }
    }

    private void OnAttributeUpdate(byte[] data)
    {
        try {
            var msg = AttributeUpdatePush.Parser.ParseFrom(data);
            Debug.Log($"[Combat] {msg.TargetId} took {msg.Damage} dmg. HP: {msg.CurrentHp}/{msg.MaxHp}");
            if (msg.IsDead) Debug.Log($"[Combat] {msg.TargetId} is DEAD");
            // TODO: Show floating damage text and update health bar
        } catch (System.Exception e) { Debug.LogError($"[Combat] Parse error: {e}"); }
    }

    private void OnPlayerDead(byte[] data)
    {
        try {
            var msg = PlayerDeadPush.Parser.ParseFrom(data);
            Debug.Log($"[Combat] {msg.Id} was killed by {msg.KillerId}");
            // TODO: Play death animation and disable control if self
        } catch (System.Exception e) { Debug.LogError($"[Combat] Parse error: {e}"); }
    }
    
    void OnDestroy() {
        if (_client != null) {
            _client.UnregisterHandler("OnSkillCast");
            _client.UnregisterHandler("OnAttributeUpdate");
            _client.UnregisterHandler("OnPlayerDead");
        }
    }
}
