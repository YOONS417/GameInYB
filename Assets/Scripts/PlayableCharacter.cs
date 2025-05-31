using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayableCharacterData : CharacterData
{
    public PlayableCharacterData(CharacterData data) : base(data.UnitName)
    {
        Spd = data.Spd;
        HP = data.HP;
        MaxHP = data.MaxHP;
        Atk = data.Atk;
        Ats = data.Ats;
        Def = data.Def;
    }
    protected int jCnt; // jump count
    public int JumpCnt
    {
        get => jCnt;
        set
        {
            if (value < 0)
                jCnt = 0;
            else
                jCnt = value;
        }
    }
    protected float jumpPower; // jump power
    public float JumpPower
    {
        get => jumpPower;
        set
        {
            if (value < 0)
                jumpPower = 0;
            else
                jumpPower = value;
        }
    }
    protected float cri;// critical chance
    public float Cri
    {
        get => cri;
        set
        {
            if (value < 0.0f)
                cri = 0.0f;
            else if (value > 1.0f)
                cri = 1.0f;
            else
                cri = value;
        }
    }
    protected float criDmg; // critical damage
    public float CriDmg
    {
        get => criDmg;
        set
        {
            if (value < 1.0f)
                criDmg = 1.0f;
            else
                criDmg = value;
        }
    }
    public PlayableCharacterData SetJCnt(int jCnt = 2)
    {
        this.JumpCnt = jCnt;
        return this;
    }
    public PlayableCharacterData SetJPow(float jumpPower = 10.0f)
    {
        this.JumpPower = jumpPower;
        return this;
    }
    public PlayableCharacterData SetCri(float cri = 0.0f)
    {
        this.Cri = cri;
        return this;
    }
    public PlayableCharacterData SetCriDmg(float criDmg = 1.5f)
    {
        this.CriDmg = criDmg;
        return this;
    }
    public override string ToString()
    {
        return $"{base.ToString()}\nJump Count : {JumpCnt}\nJump Power : {JumpPower}\nCritical Chance : {Cri}\nCritical Damage : {CriDmg}";
    }
}
public class PlayableCharacter : Character
{
    protected PlayableCharacterData Data => (PlayableCharacterData)data;
    public int d;
    public static PlayableCharacter Inst { get; set; }
    private Image messagePortrait, imageOnMessage;
    private TextMeshProUGUI unitName, message;
    public GameObject messageObj;
    private RectTransform messageBox;
    private InputSystem_Actions inputAction;
    private const float hpBarSpd = 5.0f;
    private Camera cam;
    [SerializeField]
    private SlicedFilledImage hpBar;
    [SerializeField]
    private SlicedFilledImage hpBarSec;

    [SerializeField]
    private TextMeshProUGUI hpVal;

    [SerializeField]
    protected Transform arm;
    private SpriteRenderer weaponSprite;
    private Weapon weaponScript;
    private bool isDropdown;
    private bool isHealing;
    private Coroutine hpSmooth;
    protected override void Awake()
    {
        if (PlayableCharacter.Inst != null && PlayableCharacter.Inst != this)
        {
            Destroy(PlayableCharacter.Inst);
            return;
        }
        PlayableCharacter.Inst = this;
        DontDestroyOnLoad(gameObject);
        data = new PlayableCharacterData(new CharacterData("Player"))
            .SetJCnt(2)
            .SetJPow(12.0f)
            .SetCri(0.1f)
            .SetCriDmg(1.5f);

        inputAction = new();
        jumpCnt = 2;
        weaponSprite = arm.GetChild(0).GetComponent<SpriteRenderer>();
        weaponScript = weaponSprite.GetComponent<Weapon>();
        SetupMessageBox();
        cam = Camera.main;
    }
    void OnEnable()
    {
        inputAction.Enable();
        inputAction.Player.Move.performed += OnMovement;
        inputAction.Player.Move.canceled += OnMovement;
        inputAction.Player.Jump.performed += OnJump;
        inputAction.Player.Attack.performed += OnAttack;
        inputAction.Player.Dropdown.performed += OnDropdown;
    }
    private void OnDropdown(InputAction.CallbackContext context)
    {
        if (landingLayer == LAYER.PLATFORM)
        {
            col.isTrigger = true;
            isDropdown = true;
        }
    }
    private void OnAttack(InputAction.CallbackContext context)
    {
        Attack();
    }
    protected override void Attack()
    {
        base.Attack();
        weaponScript.StartSwing();
    }
    protected override void Update()
    {
        base.Update();
        Vector3 dir = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float armAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (!weaponScript.isSwing)
        {
            if (armAngle + 90 <= 180 && armAngle + 90 >= 0)
                arm.rotation = Quaternion.Euler(0, 180, -(armAngle + 90));
            else
                arm.rotation = Quaternion.Euler(0, 0, armAngle + 90);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            SetHP(Random.Range(0, Data.MaxHP));
        }
    }
    IEnumerator HpBarFillsSmooth(SlicedFilledImage bar)
    {
        yield return new WaitForSeconds(0.3f);
        while (Mathf.Abs(bar.fillAmount - (float)Mathf.FloorToInt(Data.HP) / Mathf.FloorToInt(Data.MaxHP)) > 0.01f)
        {
            bar.fillAmount = Mathf.Lerp(bar.fillAmount, (float)Mathf.FloorToInt(Data.HP) / Mathf.FloorToInt(Data.MaxHP), Time.deltaTime * hpBarSpd);
            yield return null;
        }
        bar.fillAmount = (float)Mathf.FloorToInt(Data.HP) / Mathf.FloorToInt(Data.MaxHP);
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        Vector3 pos = transform.position;
        cam.transform.position = Vector3.Lerp(cam.transform.position, new(Mathf.Clamp(pos.x, 0, 31), Mathf.Clamp(pos.y, 0, 18), -10), Time.fixedDeltaTime * 2);
    }
    public void OnMovement(InputAction.CallbackContext context)
    {
        moveVec = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (jumpCnt > 0 && context.performed)
        {
            rigid.gravityScale = 1.5f;
            isJump = true;
            rigid.linearVelocity = new Vector2(rigid.linearVelocity.x, Data.JumpPower);
            jumpCnt--;
        }
    }
    private void SetupMessageBox()
    {
        messagePortrait = messageObj.transform.Find("portraitBox").Find("portrait").GetComponent<Image>();
        unitName = messageObj.transform.Find("portraitBox").Find("unitNameBox").Find("unitName").GetComponent<TextMeshProUGUI>();
        message = messageObj.transform.Find("messageBox").Find("message").GetComponent<TextMeshProUGUI>();
        messageBox = message.transform.parent.GetComponent<RectTransform>();
        imageOnMessage = messageObj.transform.Find("imageBox").Find("image").GetComponent<Image>();
    }
    public void ShowMessage(string[] info)
    {
        messageObj.SetActive(true);
        if (string.IsNullOrEmpty(info[0]))
        {
            messagePortrait.transform.parent.gameObject.SetActive(false);
            messageBox.offsetMin = new(50.0f, messageBox.offsetMin.y);
        }
        else
        {
            messageBox.offsetMin = new(318f, messageBox.offsetMin.y);
            messagePortrait.transform.parent.gameObject.SetActive(true);
            messagePortrait.sprite = null;
            unitName.text = info[1];
            message.text = info[2];
        }
        if (string.IsNullOrEmpty(info[3]))
        {
            imageOnMessage.transform.parent.gameObject.SetActive(false);
        }
        else
        {
            imageOnMessage.transform.parent.gameObject.SetActive(true);
            imageOnMessage.sprite = null;
        }
    }
    public void ShowMessage()
    {
        messageObj.SetActive(false);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        RaycastHit2D h = Physics2D.Raycast(foot.position, Vector2.down, rayDistance, LayerMask.GetMask("Floor", "Platform"));
        if (h && !isDropdown && rigid.linearVelocityY <= 0.0f)
        {
            Landing((LAYER)collision.gameObject.layer);
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer == (int)LAYER.PLATFORM && isDropdown)
        {
            isDropdown = false;
        }
    }
    public void SetHP(int value)
    {
        isHealing = Mathf.FloorToInt(value) > Mathf.FloorToInt(Data.HP);

        Data.HP = value;
        hpVal.text = $"{Mathf.FloorToInt(value)} / {Mathf.FloorToInt(Data.MaxHP)}";
        //HpBar fills out smoothly
        if (hpSmooth != null)
            StopCoroutine(hpSmooth);
        if (isHealing)
        {
            hpBar.fillAmount = (float)Mathf.FloorToInt(value) / Mathf.FloorToInt(Data.MaxHP);
            hpSmooth = StartCoroutine(HpBarFillsSmooth(hpBarSec));
        }
        else
        {
            hpBarSec.fillAmount = (float)Mathf.FloorToInt(value) / Mathf.FloorToInt(Data.MaxHP);
            hpSmooth = StartCoroutine(HpBarFillsSmooth(hpBar));
        }
    }
}