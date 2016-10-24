﻿#if UNITY_5 && (!UNITY_5_0 && !UNITY_5_1 && !UNITY_5_2 && ! UNITY_5_3) || UNITY_6
#define UNITY_MIN_5_4
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// left joysticker
using UnityStandardAssets.CrossPlatformInput;

// right joysticker
using CnControls;



using System.Collections;

namespace Com.EW.MyGame
{
	/// <summary>
	/// Player manager. 
	/// Handles fire Input and Beams.
	/// </summary>
	public class PlayerManager : Photon.PunBehaviour, IPunObservable
	{
		
		#region Public Variables


		[Tooltip ("The current Health of our player")]
		public float Health = 1f;

		[Tooltip ("The local player instance. Use this to know if the local player is represented in the Scene")]
		public static GameObject LocalPlayerInstance;
		public static string LocalPlayerType;


		[Tooltip ("The Player's UI GameObject Prefab")]
		public GameObject PlayerUiPrefab;

		[Tooltip ("The Player's Score GameObject Prefab")]
		public GameObject PlayerScorePrefab;

		[Tooltip ("Total Damage the player dealt to others")]
		public float DamageDealt;

		[Tooltip ("Total Damage taken from other players")]
		public float DamageTaken;



		// Audio
		public AudioClip CollisionAudio;
		private AudioSource audioSource;

		public float BulletSpeed = 150f;

		public bool IsFiring;
		public bool UsingUltra;

		#endregion

		#region Private Variables

		public float timeBetweenShots = 1f;

		private float nextShotTime = 0.0f;
		private float nextContinuesDamageTime = 0.0f;

		private static string localWeaponPrefabName;

		private static string fireBallPrefabName = "FireBall";
		private static string electricArcPrefabName = "ElectricArc";
		private static string iceCrystalPrefabName = "IceCrystal";
		private static string stoneChargePrefabName = "StoneCharge";
		private static string rancherSwordPrefabName = "RancherSword";
		private static string electricFieldPrefabName = "ElectricField";

		private string myBulletKeyName = "MyBullet";

		#endregion

		#region MonoBehaviour CallBacks

		/// <summary>
		/// MonoBehaviour method called on GameObject by Unity during early initialization phase.
		/// </summary>
		void Awake ()
		{
			// #Important
			// used in GameManager.cs: we keep track of the localPlayer instance to prevent instantiation when levels are synchronized
			if (photonView.isMine) {
				Debug.Log ("PlayerManager Awake");
				PlayerManager.LocalPlayerInstance = this.gameObject;
				myBulletKeyName = PhotonNetwork.playerName + "_Bullet";
				switch (PlayerManager.LocalPlayerType) { // set at GameManager.cs: Start()
				case "FireElement":
					localWeaponPrefabName = fireBallPrefabName;
					break;
				case "ElectricElement":
					localWeaponPrefabName = electricArcPrefabName;
					break;
				case "RancherElement":
					localWeaponPrefabName = rancherSwordPrefabName;
					break;
				case "IceElement":
					localWeaponPrefabName = iceCrystalPrefabName;
					break;
				case "StoneElement":
					localWeaponPrefabName = stoneChargePrefabName;
					break;
				default:
					localWeaponPrefabName = fireBallPrefabName;
					break;
				}
			}
			// #Critical
			// we flag as don't destroy on load so that instance survives level synchronization, thus giving a seamless experience when levels load.
			DontDestroyOnLoad (this.gameObject);

		}

		/// <summary>
		/// MonoBehaviour method called on GameObject by Unity during initialization phase.
		/// </summary>
		void Start ()
		{	// camera work
			CameraWork _cameraWork = this.gameObject.GetComponent<CameraWork> ();
			audioSource = GetComponent<AudioSource> ();

			if (_cameraWork != null) {
				if (photonView.isMine) {
					_cameraWork.OnStartFollowing ();
				}
			} else {
				Debug.LogError ("<Color=Red><a>Missing</a></Color> CameraWork Component on playerPrefab.", this);
			}


			// player UI
			if (PlayerUiPrefab != null) {
				GameObject _uiGo = Instantiate (PlayerUiPrefab) as GameObject;
				_uiGo.SendMessage ("SetTarget", this, SendMessageOptions.RequireReceiver);
			} else {
				Debug.LogWarning ("<Color=Red><a>Missing</a></Color> PlayerUiPrefab reference on player Prefab.", this);
			}


			// player Score
			if (PlayerScorePrefab != null) {
				GameObject _uiGo = Instantiate (PlayerScorePrefab) as GameObject;
				_uiGo.SendMessage ("SetTarget", this, SendMessageOptions.RequireReceiver);
			} else {
				Debug.LogWarning ("<Color=Red><a>Missing</a></Color> PlayerScorePrefab reference on player Prefab.", this);
			}



			// 
			#if UNITY_MIN_5_4
			// Unity 5.4 has a new scene management. register a method to call CalledOnLevelWasLoaded.
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, loadingMode) => {
				this.CalledOnLevelWasLoaded (scene.buildIndex);
			};
			#endif
		}

		/// <summary>
		/// MonoBehaviour method called on GameObject by Unity on every frame.
		/// </summary>
		void Update ()
		{	
			if (photonView.isMine == false && PhotonNetwork.connected == true) {
				return;
			}



			// if health less than 0, leave game
			if (Health <= 0f) {
				GameManager.Instance.LeaveRoom ();
			}


			ProcessInputs ();
			if (IsFiring) {
				if (nextShotTime <= Time.time) {
					CmdShoot ();
					nextShotTime = Time.time + timeBetweenShots;
				}
			}
			if (UsingUltra) {
				// if energe is enough
				// decrease energe bar
				UseUltra ();
			}
			return;

		}


		#if !UNITY_MIN_5_4
		/// <summary>See CalledOnLevelWasLoaded. Outdated in Unity 5.4.</summary>
		void OnLevelWasLoaded(int level)
		{
		this.CalledOnLevelWasLoaded(level);
		}
		#endif


		void CalledOnLevelWasLoaded (int level)
		{
			// check if we are outside the Arena and if it's the case, spawn around the center of the arena in a safe zone
//			if (!Physics.Raycast (transform.position, -Vector3.up, 5f)) {
//				transform.position = new Vector3 (0f, 5f, 0f);
//			}

			GameObject _uiGo = Instantiate (this.PlayerUiPrefab) as GameObject;
			_uiGo.SendMessage ("SetTarget", this, SendMessageOptions.RequireReceiver);

			// player Score
			GameObject _uiGo1 = Instantiate (this.PlayerScorePrefab) as GameObject;
			_uiGo1.SendMessage ("SetTarget", this, SendMessageOptions.RequireReceiver);
		


		}

	


		// The player take damage here
		void OnTriggerEnter2D (Collider2D obj)
		{
			Debug.Log ("PlayerManager: OnTriggerEnter2D");
			if (!photonView.isMine) {
				return;
			}

			Debug.Log (obj);

			// if player hit by bullet
			if (obj.CompareTag ("Bullet") && !obj.name.Contains (myBulletKeyName)) {
				Debug.Log ("Player is hitted by bullet");
				PhotonNetwork.Destroy (obj.GetComponent <PhotonView> ());
				Destroy (obj);

				Health -= 0.1f;
				// update damage taken
				DamageTaken += 0.1f;
			}


			// if player collide with obstacle
			if (obj.CompareTag ("Obstacle")) {
				Debug.Log ("-------Player is hitted by Obstacle");
				if (UsingUltra && PlayerManager.LocalPlayerType.Equals ("ElectricElement")) {
					return; // electric field
				}
				audioSource.PlayOneShot (CollisionAudio);
				Health -= 0.05f;
				// update damage taken
				DamageTaken += 0.05f;
			}
			if (obj.CompareTag ("HealthPack")) {
				Debug.Log ("######Player is hitted by HealthPack");
				/*
				if (UsingUltra && PlayerManager.LocalPlayerType.Equals ("ElectricElement")) {
					return; // electric field
				}*/
				PhotonNetwork.Destroy (obj.GetComponent <PhotonView> ());
				Destroy (obj);
				audioSource.PlayOneShot (CollisionAudio);//find heal
				Health += 0.5f;
				Debug.Log ("+++ HealthPack");
				if (Health > 1f) {
					Health = 1f;
					Debug.Log ("1+++ HealthPack");
				}
			}

			// Deal with damage dealt





			if (obj.CompareTag ("ElectricField") && !obj.name.Contains (myBulletKeyName)) {
				Debug.Log ("Player is hitted by others ElectricField");
				Health -= 0.05f;
			}

		}

		void OnTriggerStay2D (Collider2D obj)
		{
			if (Time.time > nextContinuesDamageTime) {
				Debug.Log ("OnTriggerStay2D: " + obj.name);
				nextContinuesDamageTime = Time.time + timeBetweenShots;
				if (obj.CompareTag ("ElectricField") && !obj.name.Contains (myBulletKeyName)) {
					Debug.Log ("Player is hitted by others ElectricField");
					Health -= 0.01f;
				}
			}

		}



		#endregion

		#region Custom

		/// <summary>
		/// Processes the inputs. Maintain a flag representing when the user is pressing Fire.
		/// </summary>
		void ProcessInputs ()
		{
			Vector2 shootVec = new Vector2 (CnInputManager.GetAxis ("Horizontal"), CnInputManager.GetAxis ("Vertical"));
			if (shootVec.magnitude > 0) {
				IsFiring = true;
			} else {
				IsFiring = false;
			}

			if (CrossPlatformInputManager.GetButtonUp ("Ultra")) {
				UsingUltra = true;
			} else {
				UsingUltra = false;
			}

		}

		void CmdShoot ()
		{
			Debug.Log ("CmdShoot is called");
			Vector2 shootVec = new Vector2 (CnInputManager.GetAxis ("Horizontal"), CnInputManager.GetAxis ("Vertical"));
			float angle = Mathf.Atan2 (shootVec.y, shootVec.x) * Mathf.Rad2Deg - 90f;

			Rigidbody2D rb2d = LocalPlayerInstance.GetComponent <Rigidbody2D> ();
			float tmp = Mathf.Sqrt (shootVec.x * shootVec.x + shootVec.y * shootVec.y) * 1.5f;

			Vector3 offset = new Vector3 (shootVec.x / tmp, shootVec.y / tmp, 0);
			Vector3 position = rb2d.position; // make sure bullet is in front of current element
			position = position + offset;

			GameObject copy = PhotonNetwork.Instantiate (localWeaponPrefabName, position, Quaternion.identity, 0);

			Rigidbody2D body = copy.GetComponent <Rigidbody2D> ();
			body.rotation = angle;
			body.AddForce (shootVec.normalized * BulletSpeed);

			Collider2D collider = copy.GetComponent <Collider2D> ();
			collider.name = myBulletKeyName;
		}

		void UseUltra ()
		{
			Debug.Log ("UsingUltra is called");
			Vector2 shootVec = new Vector2 (CnInputManager.GetAxis ("Horizontal"), CnInputManager.GetAxis ("Vertical"));
			float angle = Mathf.Atan2 (shootVec.y, shootVec.x) * Mathf.Rad2Deg - 90f;

			Rigidbody2D rb2d = LocalPlayerInstance.GetComponent <Rigidbody2D> ();
			Vector3 position = rb2d.position;

			switch (PlayerManager.LocalPlayerType) { // set at GameManager.cs: Start()
			case "FireElement":
				for (float rotation = 0; rotation < 360; rotation += 30) {
					GameObject copy = PhotonNetwork.Instantiate (localWeaponPrefabName, position, Quaternion.identity, 0);
					Rigidbody2D body = copy.GetComponent <Rigidbody2D> ();
					Collider2D collider = copy.GetComponent <Collider2D> ();
					collider.name = myBulletKeyName;
					float radians = rotation * Mathf.Deg2Rad;
					Vector2 direction = new Vector2 (Mathf.Sin (radians), Mathf.Cos (radians));
					body.rotation = rotation;
					body.AddForce (direction * BulletSpeed);
				}
				break;
			case "ElectricElement":
				GameObject field = PhotonNetwork.Instantiate (electricFieldPrefabName, position, Quaternion.identity, 0);
//				field.transform.parent = transform;
				gameObject.GetComponent<PhotonView> ().RPC ("SetElectricFieldParent", PhotonTargets.AllBuffered, field);
//				photonView.RPC ("SetElectricFieldParent", PhotonTargets.All, field);
//				field.transform.parent = LocalPlayerInstance.transform;
//				Collider2D fieldCollider = field.GetComponent <Collider2D> ();
//				fieldCollider.name = myBulletKeyName;
				break;
			case "RancherElement":
				// TODO
				break;
			case "IceElement":
				// TODO
				break;
			case "StoneElement":
				// TODO
				break;
			default:
				// TODO
				break;
			}
		}

		[PunRPC]
		void SetElectricFieldParent (GameObject field)
		{
//			Collider2D fieldCollider = field.GetComponent <Collider2D> ();
//			fieldCollider.name = myBulletKeyName;
			Debug.Log ("RPC: SetElectricFieldParent");
			field.transform.SetParent (transform);
		}


		public void showPlayerStatus() {
			
		}

		#endregion

		#region IPunObservable implementation

		void IPunObservable.OnPhotonSerializeView (PhotonStream stream, PhotonMessageInfo info)
		{
			if (stream.isWriting) {
				// We own this player: send the others our data
				stream.SendNext (IsFiring);
				stream.SendNext (Health);
				stream.SendNext (UsingUltra);
			} else {
				// Network player, receive data
				this.IsFiring = (bool)stream.ReceiveNext ();
				this.Health = (float)stream.ReceiveNext ();
				this.UsingUltra = (bool)stream.ReceiveNext ();
			}
		}

		#endregion
	}
}
