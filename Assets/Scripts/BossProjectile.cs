using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    public float speed = 8f;
    public float lifeTime = 4f;
    public float damage = 20f;   // More than a normal enemy (15), no longer an instant restart
    private Vector3 moveDirection;

    void Start()
    {
        Destroy(gameObject, lifeTime);

        // Find the player at the exact moment the projectile is fired
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            moveDirection = (player.transform.position - transform.position).normalized;
        }
        else
        {
            moveDirection = Vector3.down; // Fallback direction
        }
    }

    void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;

        // Lock Z axis to stay on the 2D plane
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerMovement pm = other.GetComponent<PlayerMovement>();
            if (pm != null) pm.TakeHit(damage, transform.position);
            Destroy(gameObject);
        }
        else if (other.CompareTag("DeathWall") || other.CompareTag("Harpoon"))
        {
            Destroy(gameObject);
        }
    }
}
