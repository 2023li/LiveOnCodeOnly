using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System;
using Moyo.Unity;
using Sirenix.OdinInspector;

public class AudioManager : MonoSingleton<AudioManager>
{

    public class AudioSaveData
    {
       public float MasterVolume=0.7f;
       public float EnvironmentVolume=1f;
       public float MusicVolume =1f;
       public float SFXVolume = 1f;
       public float VoiceVolume = 1f;

        internal static AudioSaveData GetDef()
        {
            return new AudioSaveData();
        }
    }

    // VCA引用
    private VCA vcaEnvironment;
    private VCA vcaMusic;
    private VCA vcaSFX;
    private VCA vcaVoice;

   

    // 当前音量
    private float currentMasterVolume;
    private float currentEnvironmentVolume;
    private float currentMusicVolume;
    private float currentSFXVolume;
    private float currentVoiceVolume;

    // 事件
    public event Action OnVolumeChanged;


    //第一次播放的时候初始化VAC
    private bool isInitVAC = false;
    private void InitVAC()
    {
        if (isInitVAC)
        {
            return;
        }


        // 获取所有VCA
        vcaEnvironment = RuntimeManager.GetVCA("vca:/VCA_Environment");
        vcaMusic = RuntimeManager.GetVCA("vca:/VCA_Music");
        vcaSFX = RuntimeManager.GetVCA("vca:/VCA_SFX");
        vcaVoice = RuntimeManager.GetVCA("vca:/VCA_Music");

        // 验证VCA
        ValidateVCAs();

        // 加载保存的设置或使用默认值
        LoadVolumeSettings();

        // 应用初始音量
        ApplyAllVolumes();

        isInitVAC = true;   

        Debug.Log("VACinit");
    }

    void ValidateVCAs()
    {
        if (!vcaEnvironment.isValid()) Debug.LogWarning("VCA_Environment无效");
        if (!vcaMusic.isValid()) Debug.LogWarning("VCA_Music无效");
        if (!vcaSFX.isValid()) Debug.LogWarning("VCA_SFX无效");
        if (!vcaVoice.isValid()) Debug.LogWarning("VCA_Voice无效");
    }

    #region 音量设置方法

    // 设置主音量（影响所有其他VCA）
    public void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        currentMasterVolume = volume;

        // 设置主VCA

        // 重新计算并设置其他VCA的实际音量
        UpdateAndApplyAllVolumes();

        OnVolumeChanged?.Invoke();
    }

    // 设置环境音量（独立设置）
    public void SetEnvironmentVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        currentEnvironmentVolume = volume;
        UpdateAndApplyEnvironmentVolume();

        OnVolumeChanged?.Invoke();
    }

    // 设置音乐音量（独立设置）
    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        currentMusicVolume = volume;
        UpdateAndApplyMusicVolume();

        OnVolumeChanged?.Invoke();
    }

    // 设置音效音量（独立设置）
    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        currentSFXVolume = volume;
        UpdateAndApplySFXVolume();

        OnVolumeChanged?.Invoke();
    }

    // 设置语音音量（独立设置）
    public void SetVoiceVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        currentVoiceVolume = volume;
        UpdateAndApplyVoiceVolume();

        OnVolumeChanged?.Invoke();
    }

    #endregion

    #region 实际音量计算和设置

    private void UpdateAndApplyAllVolumes()
    {
        UpdateAndApplyEnvironmentVolume();
        UpdateAndApplyMusicVolume();
        UpdateAndApplySFXVolume();
        UpdateAndApplyVoiceVolume();
    }

    private void UpdateAndApplyEnvironmentVolume()
    {
        float actualVolume = currentEnvironmentVolume * currentMasterVolume;
        vcaEnvironment.setVolume(actualVolume);
    }

    private void UpdateAndApplyMusicVolume()
    {
        float actualVolume = currentMusicVolume * currentMasterVolume;
        vcaMusic.setVolume(actualVolume);
    }

    private void UpdateAndApplySFXVolume()
    {
        float actualVolume = currentSFXVolume * currentMasterVolume;
        vcaSFX.setVolume(actualVolume);
    }

    private void UpdateAndApplyVoiceVolume()
    {
        float actualVolume = currentVoiceVolume * currentMasterVolume;
        vcaVoice.setVolume(actualVolume);
    }

    private void ApplyAllVolumes()
    {
        UpdateAndApplyAllVolumes();
    }

    #endregion

    #region 获取音量

    public float GetMasterVolume() => currentMasterVolume;
    public float GetEnvironmentVolume() => currentEnvironmentVolume;
    public float GetMusicVolume() => currentMusicVolume;
    public float GetSFXVolume() => currentSFXVolume;
    public float GetVoiceVolume() => currentVoiceVolume;

    // 获取实际音量（包含主音量影响）
    public float GetActualEnvironmentVolume() => currentEnvironmentVolume * currentMasterVolume;
    public float GetActualMusicVolume() => currentMusicVolume * currentMasterVolume;
    public float GetActualSFXVolume() => currentSFXVolume * currentMasterVolume;
    public float GetActualVoiceVolume() => currentVoiceVolume * currentMasterVolume;

    #endregion

    public void PlayOneShot(EventReference soundReference)
    {
        InitVAC();

        if (!soundReference.IsNull)
        {
            RuntimeManager.PlayOneShot(soundReference);
        }
    }
    /// <summary>
    /// 播放 3D 单次音效 (爆炸, 脚步声, 枪声) - 指定位置
    /// </summary>
    /// <param name="soundReference">FMOD Event Reference</param>
    /// <param name="worldPos">世界坐标</param>
    public void PlayOneShot(EventReference soundReference, Vector3 worldPos)
    {
        InitVAC();
        if (!soundReference.IsNull)
        {
            RuntimeManager.PlayOneShot(soundReference, worldPos);
        }
    }
    // 存储当前的背景音乐实例，以便我们需要停止或更改它
    private EventInstance musicEventInstance;
    public void InitializeMusic(EventReference musicReference)
    {
        InitVAC();
        // 如果当前有音乐在播放，先停止它
        StopMusic(true);

        musicEventInstance = RuntimeManager.CreateInstance(musicReference);
        musicEventInstance.start();
        // 如果你的BGM需要释放内存（通常BGM是在停止时释放），Release会在Stop时处理
    }
    public void StopMusic(bool allowFadeOut)
    {
        PLAYBACK_STATE state;
        musicEventInstance.getPlaybackState(out state);

        if (state != PLAYBACK_STATE.STOPPED)
        {
            musicEventInstance.stop(allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
            musicEventInstance.release(); // 重要：释放实例以清理内存
        }
    }
    // 存储当前的环境音实例 (Ambience)
    private EventInstance ambienceEventInstance;

    // 类似的逻辑可以用于环境音 (Ambience)
    public void InitializeEnvironment(EventReference ambienceReference)
    {
        InitVAC();
        StopEnvironment(true);
        ambienceEventInstance = RuntimeManager.CreateInstance(ambienceReference);
        ambienceEventInstance.start();
    }

    public void StopEnvironment(bool allowFadeOut)
    {
        ambienceEventInstance.stop(allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        ambienceEventInstance.release();
    }
    #region 保存/加载

    private void SaveVolumeSettings()
    {
        var audioSetting = PersistentManager.Instance.appData.audioSaveData;
        audioSetting.MasterVolume = currentMasterVolume;
        audioSetting.EnvironmentVolume = currentEnvironmentVolume;
        audioSetting.VoiceVolume = currentVoiceVolume;
        audioSetting.MusicVolume = currentMusicVolume;
        audioSetting.SFXVolume = currentSFXVolume;
    }

    private void LoadVolumeSettings()
    {
        var audioSetting = PersistentManager.Instance.appData.audioSaveData;
        currentMasterVolume = audioSetting.MasterVolume;
        currentMusicVolume = audioSetting.MusicVolume;
        currentSFXVolume = audioSetting.SFXVolume;
        currentEnvironmentVolume = audioSetting.EnvironmentVolume;
        currentVoiceVolume = audioSetting.VoiceVolume;
    }

    // 重置为默认值
    public void ResetToDefaults()
    {
        currentMasterVolume = 1;
        currentEnvironmentVolume = 1;
        currentMusicVolume = 1;
        currentSFXVolume = 1;
        currentVoiceVolume = 1;

        ApplyAllVolumes();
        SaveVolumeSettings();
        OnVolumeChanged?.Invoke();
    }

   

    #endregion

   
}
