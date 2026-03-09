using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponPickup : MonoBehaviour
{
    public GameObject weaponPrefab; // Reference to the prefab to equip
    public float pickupRange = 2.0f;
    
    private Transform _playerTransform;
    private PlayerAttack _playerAttack;
    private bool _canPickup = false;

    void Update()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                _playerAttack = player.GetComponent<PlayerAttack>();
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        
        if (dist <= pickupRange)
        {
            _canPickup = true;
            // Check for E key
            // Using new Input System directly or Legacy for simplicity in this hybrid setup
            // Let's use Keyboard.current
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Pickup();
            }
        }
        else
        {
            _canPickup = false;
        }
    }

    void OnGUI()
    {
        if (_canPickup && !_playerAttack.hasWeapon)
        {
            GUI.Label(new Rect(Screen.width / 2 - 50, Screen.height / 2 + 50, 200, 30), "Press 'E' to Pickup Knife");
        }
    }

    private void Pickup()
    {
        if (_playerAttack != null)
        {
            _playerAttack.EquipWeapon(weaponPrefab);
            Destroy(gameObject); // Destroy the ground object
        }
    }
}
