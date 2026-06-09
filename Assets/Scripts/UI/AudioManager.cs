using UnityEngine;
using System.Collections; // IMPORTANTE: Necessário para usar IEnumerator/Coroutines
using UnityEngine.SceneManagement; // Necessário para escutar as cenas
using UnityEngine.UI; // Necessário para acessar os Botões

public class AudioManager : MonoBehaviour
{
    // Instância estática para o padrão Singleton
    public static AudioManager Instance { get; private set; }

    [Header("---- Audio Sources ----")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("---- Audio Clips ----")]
    public AudioClip somClique;
    public AudioClip somFechar;
    public AudioClip somMoeda;
    public AudioClip somGema;
    public AudioClip somSelecionar;
    public AudioClip somMatchSucesso;
    public AudioClip somMatchFalhou;
    public AudioClip somTicTac;
    public AudioClip somPowerOn;
    public AudioClip somPowerOff;
    public AudioClip somAcertou;
    public AudioClip somErrou;



    public AudioClip musicaSplash;
    public AudioClip musicaBatalha;
    public AudioClip musicaVitoria;
    public AudioClip musicaDerrota;
    public AudioClip musicaEspera;
    public AudioClip musicaMenu;

    private Coroutine transicaoCoroutine;
    private float volumeOriginalMusica;

    private void Awake()
    {
        // Garante que só exista um AudioManager nas cenas
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Não destrói ao mudar de cena
            volumeOriginalMusica = musicSource.volume; // Salva o volume padrão definido no Inspector
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Inicia o jogo tocando a música do menu
        PlayMusic(musicaSplash);
    }

    // Método para tocar Músicas de Fundo (BGM)
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = volumeOriginalMusica;
        musicSource.Play();
    }

    // Método para tocar Efeitos Sonoros (SFX)
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        sfxSource.PlayOneShot(clip);
    }

    // Chamada pública atualizada com o parâmetro 'loop' (por padrão, será true)
    public void MudarMusicaComFade(AudioClip novoClip, float duracaoDoFade = 1.0f, bool loop = true)
    {
        // Se já houver uma transição acontecendo, interrompe para iniciar a nova
        if (transicaoCoroutine != null)
        {
            StopCoroutine(transicaoCoroutine);
        }

        // Inicia a transição passando também a configuração de loop
        transicaoCoroutine = StartCoroutine(TransicaoMusicaCoroutine(novoClip, duracaoDoFade, loop));
    }

    // Lógica interna da transição
    private IEnumerator TransicaoMusicaCoroutine(AudioClip novoClip, float duracao, bool loop)
    {
        float tempoConfigurado = duracao / 2f; 

        // 1. FADE OUT (Diminui o volume da música atual)
        if (musicSource.isPlaying)
        {
            float tempoDecorrido = 0f;
            while (tempoDecorrido < tempoConfigurado)
            {
                tempoDecorrido += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(volumeOriginalMusica, 0f, tempoDecorrido / tempoConfigurado);
                yield return null; 
            }
        }

        // 2. TROCA DE MÚSICA E APLICA O LOOP
        musicSource.clip = novoClip;
        musicSource.loop = loop; 
        
        if (novoClip != null)
        {
            musicSource.Play();

            // 3. FADE IN (Aumenta o volume da nova música)
            float tempoDecorrido = 0f;
            while (tempoDecorrido < tempoConfigurado)
            {
                tempoDecorrido += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, volumeOriginalMusica, tempoDecorrido / tempoConfigurado);
                yield return null;
            }
        }
    }

  // Métodos de atalho para os scripts do Unity
    public void IniciarMusicaVitoria() => MudarMusicaComFade(musicaVitoria, 1.0f, false);
    public void IniciarMusicaDerrota() => MudarMusicaComFade(musicaDerrota, 1.0f, false);

    public void TocarTicTac() => PlaySFX(somTicTac);
    public void TocarAcertou() => PlaySFX(somAcertou);
    public void TocarErrou() => PlaySFX(somErrou);

    private void OnEnable()
    {
        // Inscreve o AudioManager para ser avisado sempre que uma cena carregar
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    private void OnDisable()
    {
        // Desinscreve ao ser destruído (boa prática para evitar memory leaks)
        SceneManager.sceneLoaded -= AoCarregarCena;
    }

    // Esta função roda automaticamente assim que o jogador entra numa nova tela
    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        // O próprio Gerenciador decide o que fazer baseado no nome da cena!
        if (cena.name == "HomeScreen")
        {
            MudarMusicaComFade(musicaMenu);
        }
        else if (cena.name == "Match")
        {
            MudarMusicaComFade(musicaBatalha);
        }
        else if (cena.name == "MatchMaking"|| cena.name == "MatchMakingPrivate")
        {
            MudarMusicaComFade(musicaEspera);
        }
        


        // Encontra TODOS os botões ativos na cena atual de uma só vez
        Button[] todosOsBotoes = FindObjectsOfType<Button>(true);

        foreach (Button botao in todosOsBotoes)
        {
            
            if(botao.name.Contains("Sair") || botao.name.Contains("Fechar") || botao.name.Contains("Abandona") || botao.name.Contains("Close") || botao.name.Contains("Voltar") || botao.name.Contains("Back")|| botao.name.Contains("Cancel")|| botao.name.Contains("Retomar") )
            {
                // Se o nome do botão contiver "Sair" ou "Fechar", associa o som de fechar
                botao.onClick.AddListener(() => 
                {
                    Instance?.PlaySFX(Instance.somFechar); // Toca o som de fechar usando a instância do AudioManager
                });
            }
            else if(botao.name.Contains("Compra") )
            {
                // Se o nome do botão contiver "Comprar" ou "Upgrade", associa o som de moeda
                botao.onClick.AddListener(() => 
                {
                    Instance?.PlaySFX(Instance.somMoeda); // Toca o som de moeda usando a instância do AudioManager
                });
            }
            else if(botao.name.Contains("Medal") )
            {
                // Se o nome do botão contiver "Comprar" ou "Upgrade", associa o som de gema
                botao.onClick.AddListener(() => 
                {
                    Instance?.PlaySFX(Instance.somPowerOn); // Toca o som de gema usando a instância do AudioManager
                });
            }
             else if(botao.name.Contains("Opcao"))
            {
                // Se o nome do botão contiver "Selecionar" ou "Escolher", associa o som de selecionar
                botao.onClick.AddListener(() => 
                {
                    Instance?.PlaySFX(Instance.somSelecionar); // Toca o som de selecionar usando a instância do AudioManager
                });
            }
            
            // Adiciona o evento de clique via código, sem você precisar tocar no Inspector!
            botao.onClick.AddListener(() => 
            {
                Instance?.PlaySFX(Instance.somClique); // Toca o som de clique usando a instância do AudioManager
            });
        }
    }


}