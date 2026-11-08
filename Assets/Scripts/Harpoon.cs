using UnityEngine;

public class Harpoon : MonoBehaviour
{
    public float speed = 15f;
    public float lifeTime = 3f;
    public Vector3 direction = Vector3.up;
    public GameObject hitEffectPrefab; // Optional: explosion / bubble burst particle

    void Start()
    {
        // Destroy after lifetime expires if it misses the target
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // Definitive movement command
        transform.position += direction * speed * Time.deltaTime;

        // Lock Z axis to 0 to stay on the same 2D plane
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        // Enemies pop in one hit (the boss counts its own hits in BossAI)
        if (other.CompareTag("Enemy"))
        {
            GameObject enemy = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;

            // Pufferfish bursts with its own animation and blast
            PufferEnemy puffer = enemy.GetComponentInParent<PufferEnemy>();
            if (puffer != null)
            {
                puffer.Burst();
                Destroy(gameObject);
                return;
            }

            if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, enemy.transform.position, Quaternion.identity);
            AudioManager.PlaySfx(Sfx.EnemyExplode);
            Destroy(enemy);
            Destroy(gameObject);
        }
        else if (other.CompareTag("Boss"))
        {
            Destroy(gameObject);
        }
    }
}