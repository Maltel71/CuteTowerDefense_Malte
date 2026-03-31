using UnityEngine;
using System.Collections;

public class RearWheelDrive : MonoBehaviour {

    private WheelCollider[] wheels;
    private Rigidbody rb;

    public float maxAngle = 30;
    public float maxTorque = 300;
    public GameObject wheelShape;

    public AudioSource carIdleSound;
    public AudioSource carDrivingSound;
    public AudioSource gearShiftSound; // New variable for gear shift sound
    public float pitchMin = 0.0f; // Set initial pitch to 0%
    public float pitchMax = 2.0f;
    public float fadeSpeed = 1.0f;

    // New variables for volume control
    public float carIdleVolumeMin = 0.0f;
    public float carIdleVolumeMax = 1.0f;
    public float carDrivingVolumeMin = 0.0f;
    public float carDrivingVolumeMax = 1.0f;

    private bool hasShiftedUp = false;
    private bool hasShiftedDown = false;

    private void Start()
    {
        wheels = GetComponentsInChildren<WheelCollider>();
        rb = GetComponent<Rigidbody>();

        for (int i = 0; i < wheels.Length; ++i) 
        {
            var wheel = wheels[i];

            if (wheelShape != null)
            {
                var ws = GameObject.Instantiate(wheelShape);
                ws.transform.parent = wheel.transform;

                if (wheel.transform.localPosition.x < 0f)
                {
                    ws.transform.localScale = new Vector3(ws.transform.localScale.x * -1f, ws.transform.localScale.y, ws.transform.localScale.z);
                }
            }
        }

        carDrivingSound.pitch = pitchMin; // Set initial pitch to 0%
    }

    private void FixedUpdate()
    {
        float angle = maxAngle * Input.GetAxis("Horizontal");
        float torque = maxTorque * Input.GetAxis("Vertical");

        foreach (WheelCollider wheel in wheels)
        {
            if (wheel.transform.localPosition.z > 0)
                wheel.steerAngle = angle;

            if (wheel.transform.localPosition.z < 0)
                wheel.motorTorque = torque;

            if (wheelShape) 
            {
                Quaternion q;
                Vector3 p;
                wheel.GetWorldPose(out p, out q);

                Transform shapeTransform = wheel.transform.GetChild(0);
                shapeTransform.position = p;
                shapeTransform.rotation = q;
            }
        }

        UpdateEngineSound();
    }

    private void UpdateEngineSound()
    {
        float speed = rb.linearVelocity.magnitude;
        float targetVolume = Mathf.Clamp01(speed / 10.0f); // Adjust the divisor to control the fade speed

        carIdleSound.volume = Mathf.Lerp(carIdleSound.volume, Mathf.Lerp(carIdleVolumeMin, carIdleVolumeMax, 1.0f - targetVolume), Time.deltaTime * fadeSpeed);
        carDrivingSound.volume = Mathf.Lerp(carDrivingSound.volume, Mathf.Lerp(carDrivingVolumeMin, carDrivingVolumeMax, targetVolume), Time.deltaTime * fadeSpeed);

        float targetPitch = Mathf.Lerp(pitchMin, pitchMax, targetVolume);
        carDrivingSound.pitch = targetPitch;

        // Check for gear shift up
        if (targetPitch >= pitchMax && !hasShiftedUp)
        {
            StartCoroutine(PlayGearShiftSoundAfterDelay());
            StartCoroutine(ResetPitchAfterDelay());
            hasShiftedUp = true;
            hasShiftedDown = false;
        }
        // Check for gear shift down
        else if (targetPitch <= pitchMax * 0.15f && !hasShiftedDown)
        {
            StartCoroutine(PlayGearShiftSoundAfterDelay());
            StartCoroutine(ResetPitchAfterDelay());
            hasShiftedDown = true;
            hasShiftedUp = false;
        }
    }

    private IEnumerator PlayGearShiftSoundAfterDelay()
    {
        yield return new WaitForSeconds(2.0f);
        gearShiftSound.Play();
    }

    private IEnumerator ResetPitchAfterDelay()
    {
        yield return new WaitForSeconds(2.0f);
        carDrivingSound.pitch = pitchMax * 0.5f;
    }
}
