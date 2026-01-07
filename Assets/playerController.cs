using UnityEngine;

public class playerController : MonoBehaviour
{

    public Vector2Int playMatSize = new Vector2Int(5, 5);
    public Vector2Int startPoint = new Vector2Int(10, 10);
    //public GameObject hexTile;
    public GameObject GridTilePool;
    public GameObject WorldManagerObject;
    public float moveSpeed = 5.0f;
    public float MaxMoveSpeed = 50.0f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GridTilePool.GetComponent<TilePool>().player = this;
        GridTilePool.GetComponent<TilePool>().worldManager = GameObject.Find("WorldManager").GetComponent<WorldManager>();
        GridTilePool.GetComponent<TilePool>().GeneratePool(playMatSize.x * playMatSize.y);
        WorldManagerObject.GetComponent<WorldManager>().Init();

        GridTilePool.GetComponent<TilePool>().PlaceAroundCenter(new Vector2(transform.position.x, transform.position.z), playMatSize, startPoint);
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 direction = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        if (Input.GetKeyDown(KeyCode.Q))
        {
            moveSpeed -= 5.0f;
            if (moveSpeed < 5.0f)
            {
                moveSpeed = 5.0f;
            }
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            moveSpeed += 5.0f;
            if (moveSpeed > MaxMoveSpeed)
            {
                moveSpeed = MaxMoveSpeed;
            }
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                WorldManagerObject.GetComponent<WorldManager>().Regen();
                GridTilePool.GetComponent<TilePool>().PlaceAroundCenter(new Vector2(transform.position.x, transform.position.z), playMatSize, startPoint);
            }
        }

        GridTilePool.GetComponent<TilePool>().MovePlaymat(direction, Time.deltaTime, moveSpeed);
        //Scrollwheel zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            Camera.main.transform.position += Camera.main.transform.forward * scroll * 10.0f;
            if (Camera.main.transform.position.y < 5.0f)
            {
                Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, 5.0f, Camera.main.transform.position.z);
            }
            else if (Camera.main.transform.position.y > 35.0f)
            {
                Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, 35.0f, Camera.main.transform.position.z);
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
    private void FixedUpdate()
    {
    }
}
