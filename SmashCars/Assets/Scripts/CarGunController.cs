
using System.Collections;
using UnityEngine;
using TMPro;

public class CarGunController : MonoBehaviour
{
    [Header("Gun Muzzles")]
    [SerializeField] private Transform leftMuzzle;
    [SerializeField] private Transform rightMuzzle;

    [Header("Firing")]
    [SerializeField] private float fireRate = 6f;
    [SerializeField] private float range = 60f;
    [SerializeField] private float damagePerShot = 10f;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Left Missile Weapon")]
    [SerializeField] private MissileProjectile missilePrefab;
    [SerializeField] private float missileSpeed = 35f;
    [SerializeField] private float missileDamage = 35f;

    [Header("Ammo")]
    [SerializeField] private int leftMagazineSize = 24;
    [SerializeField] private int rightMagazineSize = 24;
    [SerializeField] private float reloadDuration = 1.5f;

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem muzzleFlashPrefab;
    [SerializeField] private ParticleSystem impactEffectPrefab;
    [SerializeField] private LineRenderer tracerPrefab;
    [Tooltip("Visual travel speed only; hit detection remains instant and reliable.")]
    [SerializeField] private float bulletVisualSpeed = 120f;
    [SerializeField] private float bulletVisualLength = 0.8f;

    [Header("Controls")]
    [SerializeField] private bool enablePlayerInput = true;
    [SerializeField] private KeyCode leftFireKey = KeyCode.Q;
    [SerializeField] private KeyCode rightFireKey = KeyCode.E;
    [SerializeField] private KeyCode reloadKey = KeyCode.F;

    [Header("Weapon HUD")]
    [SerializeField] private TextMeshProUGUI leftAmmoText;
    [SerializeField] private TextMeshProUGUI rightAmmoText;
    [SerializeField] private TextMeshProUGUI reloadText;

    private int leftCurrentAmmo;
    private int rightCurrentAmmo;
    private float nextLeftShotTime;
    private float nextRightShotTime;
    private bool isReloading;
    private CarHealth ownHealth;
    private readonly RaycastHit[] shotHits = new RaycastHit[32];

    public int LeftCurrentAmmo => leftCurrentAmmo;
    public int RightCurrentAmmo => rightCurrentAmmo;
    public int LeftMagazineSize => leftMagazineSize;
    public int RightMagazineSize => rightMagazineSize;
    public bool IsReloading => isReloading;

    private void Awake()
    {
        ownHealth = GetComponentInParent<CarHealth>();
        leftMagazineSize = Mathf.Max(1, leftMagazineSize);
        rightMagazineSize = Mathf.Max(1, rightMagazineSize);
        reloadDuration = Mathf.Max(0f, reloadDuration);
        leftCurrentAmmo = leftMagazineSize;
        rightCurrentAmmo = rightMagazineSize;
        leftAmmoText.text = $"LEFT: {leftCurrentAmmo}/{leftMagazineSize} (Q)";
        rightAmmoText.text = $"RIGHT: {rightCurrentAmmo}/{rightMagazineSize} (E)";
    }

    private void Update()
    {
        if (!enablePlayerInput)
            return;

        if (Input.GetKeyDown(reloadKey))
            StartReload();

        if (Input.GetKey(leftFireKey))
            TryFire(leftMuzzle, ref nextLeftShotTime);

        if (Input.GetKey(rightFireKey))
            TryFire(rightMuzzle, ref nextRightShotTime);
    }

    private void TryFire(Transform muzzle, ref float nextShotTime)
    {
        if (muzzle == null ||
            isReloading ||
            fireRate <= 0f)
        {
            return;
        }

        if ((muzzle == leftMuzzle && leftCurrentAmmo <= 0) || (muzzle == rightMuzzle && rightCurrentAmmo <= 0))
        {
            return;
        }

        if (Time.time < nextShotTime)
            return;

        nextShotTime = Time.time + 1f / fireRate;
        if (muzzle == leftMuzzle)
        {
            leftCurrentAmmo--;
            leftAmmoText.text = $"LEFT: {leftCurrentAmmo}/{leftMagazineSize} (Q)";
        }
        else if (muzzle == rightMuzzle)
        {
            rightCurrentAmmo--;
            rightAmmoText.text = $"RIGHT: {rightCurrentAmmo}/{rightMagazineSize} (E)";
        }

        // Muzzle flash
        if (muzzleFlashPrefab != null)
        {
            ParticleSystem flash = Instantiate(
                muzzleFlashPrefab,
                muzzle.position,
                muzzle.rotation
            );

            flash.Play();
            Destroy(flash.gameObject, 2f);
        }

        if (muzzle == leftMuzzle)
        {
            FireMissile(muzzle);
        }
        else
        {
            FireHitscanBullet(muzzle);
        }
    }

    private void FireHitscanBullet(Transform muzzle)
    {
        Vector3 origin = muzzle.position;
        Vector3 direction = muzzle.forward;
        Vector3 endPoint = origin + direction * range;
        bool hitSomething = false;
        Vector3 hitNormal = Vector3.up;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            shotHits,
            range,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = range;
        RaycastHit nearestHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = shotHits[i];
            CarHealth hitCar =
                hit.collider.GetComponentInParent<CarHealth>();

            // Ignore every collider belonging to our own car.
            if (hitCar != null && hitCar == ownHealth)
                continue;

            if (ownHealth != null &&
                hit.collider.transform.root == transform.root)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        if (nearestDistance < range)
        {
            endPoint = nearestHit.point;
            hitNormal = nearestHit.normal;
            hitSomething = true;

            CarHealth hitCar =
                nearestHit.collider.GetComponentInParent<CarHealth>();

            if (hitCar != null && !hitCar.IsEliminated)
            {
                Debug.Log($"Gun fired by {name}, hit {hitCar.name}");
                hitCar.TakeDamage(damagePerShot);
            }
        }

        // The raycast resolves the shot immediately, while this short moving
        // streak makes it look like a physical bullet instead of a laser beam.
        if (tracerPrefab != null)
            StartCoroutine(PlayBulletVisual(
                origin, endPoint, hitSomething, hitNormal
            ));
        else if (hitSomething)
            SpawnImpact(endPoint, hitNormal);
    }

    private IEnumerator PlayBulletVisual(
        Vector3 origin,
        Vector3 endPoint,
        bool hitSomething,
        Vector3 hitNormal)
    {
        LineRenderer tracer = Instantiate(tracerPrefab);
        tracer.useWorldSpace = true;
        tracer.positionCount = 2;

        Vector3 path = endPoint - origin;
        float distance = path.magnitude;
        Vector3 direction = distance > 0.001f ? path / distance : Vector3.forward;
        float duration = distance / Mathf.Max(bulletVisualSpeed, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float headDistance = Mathf.Min(
                distance,
                distance * elapsed / duration
            );
            float tailDistance = Mathf.Max(
                0f,
                headDistance - bulletVisualLength
            );

            tracer.SetPosition(0, origin + direction * tailDistance);
            tracer.SetPosition(1, origin + direction * headDistance);
            yield return null;
        }

        tracer.SetPosition(0, endPoint - direction * Mathf.Min(
            bulletVisualLength, distance
        ));
        tracer.SetPosition(1, endPoint);

        if (hitSomething)
            SpawnImpact(endPoint, hitNormal);

        Destroy(tracer.gameObject);
    }

    private void SpawnImpact(Vector3 position, Vector3 normal)
    {
        if (impactEffectPrefab == null)
            return;

        ParticleSystem impact = Instantiate(
            impactEffectPrefab,
            position,
            Quaternion.LookRotation(normal)
        );

        impact.Play();
        Destroy(impact.gameObject, 2f);
    }

    public void StartReload()
    {
        if (isReloading || (leftCurrentAmmo == leftMagazineSize && rightCurrentAmmo == rightMagazineSize))
            return;

        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        reloadText.text = "RELOADING...";

        yield return new WaitForSeconds(reloadDuration);

        leftCurrentAmmo = leftMagazineSize;
        rightCurrentAmmo = rightMagazineSize;
        leftAmmoText.text = $"LEFT: {leftCurrentAmmo}/{leftMagazineSize} (Q)";
        rightAmmoText.text = $"RIGHT: {rightCurrentAmmo}/{rightMagazineSize} (E)";
        reloadText.text = "RELOADED";

        yield return new WaitForSeconds(1f);

        reloadText.text = "Press (F) to RELOAD";

        isReloading = false;
    }

    private void FireMissile(Transform muzzle)
    {
        if (missilePrefab == null)
            return;

        MissileProjectile missile = Instantiate(
            missilePrefab,
            muzzle.position,
            muzzle.rotation
        );

        missile.Initialize(
            transform.root,
            missileSpeed,
            missileDamage,
            impactEffectPrefab
        );
    }
}
