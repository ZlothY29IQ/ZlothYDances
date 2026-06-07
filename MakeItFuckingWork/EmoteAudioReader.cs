using System;
using Photon.Voice;
using UnityEngine;

namespace ZlothYDances.MakeItFuckingWork;

public class EmoteAudioReader : IAudioReader<float>
{
    private const    int    MicBufferSeconds = 1;
    private readonly string device;
    private          bool   clipLoop;
    private          bool   clipPlaying;
    private          float  clipPosition;

    private float[] clipSamples;
    private float   clipStep;
    private int     lastMicPosition;

    private AudioClip micClip;
    private float[]   tempBuffer;

    private EmoteAudioReader(int samplingRate, string device = null)
    {
        SamplingRate = samplingRate;
        this.device = string.IsNullOrEmpty(device)
                              ? Microphone.devices.Length > 0 ? Microphone.devices[0] : null
                              : device;

        StartMic();
    }

    public static EmoteAudioReader Instance { get; private set; }

    public int SamplingRate { get; }

    public int    Channels => 1;
    public string Error    => null;

    public bool Read(float[] buffer)
    {
        if (micClip == null || string.IsNullOrEmpty(device))
            return false;

        int micPos = Microphone.GetPosition(device);
        int available = micPos < lastMicPosition
                                ? micClip.samples - lastMicPosition + micPos
                                : micPos                              - lastMicPosition;

        int needed = Mathf.CeilToInt(buffer.Length * (SamplingRate / (float)SamplingRate));

        if (available < needed)
            return false;

        if (tempBuffer == null || tempBuffer.Length != needed)
            tempBuffer = new float[needed];

        int remaining = micClip.samples - lastMicPosition;
        if (remaining >= needed)
        {
            micClip.GetData(tempBuffer, lastMicPosition);
        }
        else
        {
            micClip.GetData(tempBuffer, lastMicPosition);
            float[] wrap = new float[needed - remaining];
            micClip.GetData(wrap, 0);
            Array.Copy(wrap, 0, tempBuffer, remaining, wrap.Length);
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float mic = tempBuffer[i < tempBuffer.Length ? i : tempBuffer.Length - 1];

            float clip = 0f;
            if (clipPlaying && clipSamples != null)
            {
                int idx  = (int)clipPosition;
                int next = idx + 1;

                if (idx >= clipSamples.Length)
                {
                    if (clipLoop)
                    {
                        clipPosition = 0f;
                        idx           = 0;
                        next          = 1;
                    }
                    else
                    {
                        StopClip();

                        goto writeMic;
                    }
                }

                if (next >= clipSamples.Length)
                    next = clipLoop ? 0 : idx;

                float frac = clipPosition - idx;
                clip = Mathf.Lerp(clipSamples[idx], clipSamples[next], frac);

                clipPosition += clipStep;
                if (clipLoop && clipPosition >= clipSamples.Length)
                    clipPosition -= clipSamples.Length;
            }

            writeMic:
            buffer[i] = Mathf.Clamp(mic + clip, -1f, 1f);
        }

        lastMicPosition = (lastMicPosition + needed) % micClip.samples;

        return true;
    }

    public void Dispose()
    {
        StopClip();

        if (!string.IsNullOrEmpty(device) && Microphone.IsRecording(device))
            Microphone.End(device);

        micClip = null;
        Instance = null;
    }

    public static EmoteAudioReader Get(int samplingRate = 48000, string device = null)
        => Instance ??= new EmoteAudioReader(samplingRate, device);

    public void PlayClip(AudioClip clip, bool loop = true)
    {
        if (clip == null)
            return;

        int     channels = clip.channels;
        float[] raw      = new float[clip.samples * channels];
        clip.GetData(raw, 0);

        float[] mono = new float[clip.samples];
        for (int i = 0; i < clip.samples; i++)
            if (channels == 1)
            {
                mono[i] = raw[i];
            }
            else
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += raw[i * channels + c];

                mono[i] = sum / channels;
            }

        clipSamples  = mono;
        clipPosition = 0f;
        clipStep     = clip.frequency / (float)SamplingRate;
        clipLoop     = loop;
        clipPlaying  = true;
    }

    public void StopClip()
    {
        clipPlaying  = false;
        clipSamples  = null;
        clipPosition = 0f;
    }

    private void StartMic()
    {
        if (string.IsNullOrEmpty(device))
            return;

        if (Microphone.IsRecording(device))
            Microphone.End(device);

        micClip         = Microphone.Start(device, true, MicBufferSeconds, SamplingRate);
        lastMicPosition = 0;
    }
}