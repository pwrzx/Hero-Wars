using UnityEngine;

namespace TowerTrials
{
    // The occupied room is one structural body. Its enemy and target rim ride with it,
    // while the open compound collider lets the crown rest on the walls, above the enemy.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TowerSegmentPhysics : MonoBehaviour
    {
        private Rigidbody body;
        private CharacterMotion passenger;
        private CombatFeedback feedback;
        private CombatAudio audioFeedback;
        private Vector3 restPosition;
        private float gravity, fallingTime, peakSpeed;
        private int stableSteps;
        private bool supported, impactReported;
        public bool IsSettling { get; private set; }
        public int LandingCount { get; private set; }
        public int RecoveryCount { get; private set; }

        public void Configure(float roomHeight, bool crown, float fallGravity,
            CombatFeedback combatFeedback, CombatAudio combatAudio)
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = crown ? 18 : 45;
            body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.solverVelocityIterations = 6;
            body.sleepThreshold = 0;
            gravity = fallGravity;
            feedback = combatFeedback;
            audioFeedback = combatAudio;
            passenger = GetComponentInChildren<CharacterMotion>();

            AddBox(Vector3.zero, new Vector3(crown ? 2.66f : 2.46f, 0.2f, crown ? 1.64f : 1.46f));
            if (!crown)
            {
                float wallHeight = roomHeight - 0.2f;
                AddBox(new Vector3(-1.12f, roomHeight * 0.5f, 0), new Vector3(0.22f, wallHeight, 1.42f));
                AddBox(new Vector3(1.12f, roomHeight * 0.5f, 0), new Vector3(0.22f, wallHeight, 1.42f));
                AddBox(new Vector3(0, roomHeight * 0.5f, -0.6f), new Vector3(2.02f, wallHeight, 0.18f));
            }
        }

        private void AddBox(Vector3 center, Vector3 size)
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            collider.contactOffset = 0.002f;
        }

        public void DropTo(float localY)
        {
            Vector3 local = transform.localPosition;
            if (Mathf.Abs(local.y - localY) < 0.01f) return;
            restPosition = transform.parent.TransformPoint(new Vector3(local.x, localY, local.z));
            fallingTime = peakSpeed = 0;
            stableSteps = 0;
            supported = impactReported = false;
            IsSettling = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        private void FixedUpdate()
        {
            if (!IsSettling) return;
            fallingTime += Time.fixedDeltaTime;
            peakSpeed = Mathf.Max(peakSpeed, -body.linearVelocity.y);
            bool atRest = supported && Mathf.Abs(body.position.y - restPosition.y) < 0.025f &&
                Mathf.Abs(body.linearVelocity.y) < 0.2f;
            stableSteps = atRest ? stableSteps + 1 : 0;
            if (stableSteps >= 3)
            {
                LockAtRest();
                return;
            }
            // This bounded recovery protects input if external scene edits remove a support.
            if (fallingTime > 3)
            {
                RecoveryCount++;
                Debug.LogWarning("Tower segment lost its landing support: " + name, this);
                LockAtRest();
                return;
            }
            supported = false;
            body.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision) => CheckSupport(collision);
        private void OnCollisionStay(Collision collision) => CheckSupport(collision);

        private void CheckSupport(Collision collision)
        {
            if (!IsSettling) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y < 0.5f) continue;
                supported = true;
                if (impactReported || Mathf.Abs(body.position.y - restPosition.y) > 0.04f) continue;
                impactReported = true;
                LandingCount++;
                float strength = Mathf.Clamp01(peakSpeed / 10);
                if (feedback != null) feedback.Landing(contact.point);
                if (audioFeedback != null) audioFeedback.Play(CombatSound.Collapse);
                if (passenger != null) passenger.ReactToLanding(strength);
            }
        }

        private void LockAtRest()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            // Remove solver/contact skin drift only after the physical landing has completed.
            body.position = restPosition;
            IsSettling = false;
        }
    }
}
