import { Box, CircularProgress, IconButton, useTheme } from '@mui/material';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';


import { AppIcon } from '../../../../../shared/icons/AppIcon';

interface AudioWaveformProps {
  audioSrc?: string;
  disabled?: boolean;
  isGenerating?: boolean;
  isPlaying?: boolean;
  onEnded?: () => void;
  onPause?: () => void;
  onPlay?: () => void;
  resetProgress?: boolean;
}

type AudioContextConstructor = typeof AudioContext;

interface WindowWithWebkitAudioContext extends Window {
  webkitAudioContext?: AudioContextConstructor;
}

const sampleCount = 200;

export function AudioWaveform({ audioSrc, disabled = false, isGenerating = false, isPlaying, onEnded, onPause, onPlay, resetProgress = false }: AudioWaveformProps) {
  const theme = useTheme();
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const animationRef = useRef<number | null>(null);
  const [internalPlaying, setInternalPlaying] = useState(false);
  const [progress, setProgress] = useState(0);
  const [waveformData, setWaveformData] = useState<number[]>(() => generatePlaceholderWaveform());
  const playing = isPlaying ?? internalPlaying;

  const drawWaveform = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas || waveformData.length === 0) {
      return;
    }

    const context = canvas.getContext('2d');
    if (!context) {
      return;
    }

    const devicePixelRatio = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    canvas.width = rect.width * devicePixelRatio;
    canvas.height = rect.height * devicePixelRatio;
    context.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
    context.clearRect(0, 0, rect.width, rect.height);

    const barWidth = 2;
    const barSpacing = 1;
    const startX = (rect.width - waveformData.length * (barWidth + barSpacing)) / 2;
    const centerY = rect.height / 2;
    const progressIndex = Math.floor((progress / 100) * waveformData.length);

    waveformData.forEach((value, index) => {
      const barHeight = Math.max(2, (value / 100) * (rect.height * 0.8));
      const x = startX + index * (barWidth + barSpacing);
      context.fillStyle = index <= progressIndex ? theme.palette.primary.main : theme.palette.mode === 'dark' ? '#444' : '#ccc';
      context.fillRect(x, centerY - barHeight / 2, barWidth, barHeight);
    });
  }, [progress, theme.palette.mode, theme.palette.primary.main, waveformData]);

  useEffect(() => {
    let disposed = false;

    async function loadWaveform() {
      if (!audioSrc) {
        setWaveformData(generatePlaceholderWaveform());
        return;
      }

      try {
        const response = await fetch(audioSrc);
        const arrayBuffer = await response.arrayBuffer();
        const AudioContextClass = window.AudioContext ?? (window as WindowWithWebkitAudioContext).webkitAudioContext;
        if (!AudioContextClass) {
          throw new Error('AudioContext is not available.');
        }

        const audioContext = new AudioContextClass();
        const buffer = await audioContext.decodeAudioData(arrayBuffer.slice(0));
        await audioContext.close();

        if (!disposed) {
          setWaveformData(generateWaveform(buffer));
        }
      } catch {
        if (!disposed) {
          setWaveformData(generatePlaceholderWaveform());
        }
      }
    }

    void loadWaveform();

    return () => {
      disposed = true;
    };
  }, [audioSrc]);

  useEffect(() => {
    if (resetProgress) {
      setProgress(0);
    }
  }, [resetProgress]);

  useEffect(() => {
    drawWaveform();
  }, [drawWaveform]);

  useEffect(() => {
    if (!playing || !audioRef.current) {
      if (animationRef.current != null) {
        cancelAnimationFrame(animationRef.current);
        animationRef.current = null;
      }
      return;
    }

    const updateProgress = () => {
      const audio = audioRef.current;
      if (audio?.duration && !Number.isNaN(audio.duration)) {
        setProgress((audio.currentTime / audio.duration) * 100);
      }

      if (audio && !audio.paused) {
        animationRef.current = requestAnimationFrame(updateProgress);
      }
    };

    animationRef.current = requestAnimationFrame(updateProgress);

    return () => {
      if (animationRef.current != null) {
        cancelAnimationFrame(animationRef.current);
        animationRef.current = null;
      }
    };
  }, [playing]);

  const handlePlayPause = async () => {
    if (disabled || isGenerating) {
      return;
    }

    if (playing) {
      audioRef.current?.pause();
      setInternalPlaying(false);
      onPause?.();
      return;
    }

    if (audioRef.current) {
      await audioRef.current.play();
      setInternalPlaying(true);
    }
    onPlay?.();
  };

  const handleCanvasClick = (event: React.MouseEvent<HTMLCanvasElement>) => {
    const audio = audioRef.current;
    const canvas = canvasRef.current;
    if (!audio?.duration || !canvas || disabled || isGenerating) {
      return;
    }

    const rect = canvas.getBoundingClientRect();
    const clickProgress = Math.max(0, Math.min(100, ((event.clientX - rect.left) / rect.width) * 100));
    audio.currentTime = (clickProgress / 100) * audio.duration;
    setProgress(clickProgress);
  };

  const playIcon = useMemo(() => (playing ? 'pause' : 'play'), [playing]);

  return (
    <Box className="flowise-audio-waveform">
      {audioSrc ? (
        <audio
          ref={audioRef}
          src={audioSrc}
          onEnded={() => {
            setInternalPlaying(false);
            setProgress(0);
            onEnded?.();
          }}
          onLoadedMetadata={() => setProgress(0)}
          onPause={() => setInternalPlaying(false)}
        >
          <track kind="captions" />
        </audio>
      ) : null}
      <IconButton className={playing ? 'is-playing' : undefined} disabled={disabled || isGenerating} size="small" onClick={() => void handlePlayPause()}>
        {isGenerating ? <CircularProgress size={16} /> : <AppIcon name={playIcon} />}
      </IconButton>
      <canvas ref={canvasRef} height={32} width={400} className="flowise-audio-waveform-canvas" onClick={handleCanvasClick} />
    </Box>
  );
}

function generateWaveform(buffer: AudioBuffer): number[] {
  const rawData = buffer.getChannelData(0);
  const blockSize = Math.max(1, Math.floor(rawData.length / sampleCount));
  const filteredData: number[] = [];

  for (let index = 0; index < sampleCount; index += 1) {
    let sum = 0;
    for (let blockIndex = 0; blockIndex < blockSize; blockIndex += 1) {
      sum += Math.abs(rawData[index * blockSize + blockIndex] ?? 0);
    }
    filteredData.push(sum / blockSize);
  }

  const maxValue = Math.max(...filteredData, 1);
  return filteredData.map((value) => (value / maxValue) * 100);
}

function generatePlaceholderWaveform(): number[] {
  return Array.from({ length: sampleCount }, (_, index) => {
    const position = index / sampleCount;
    const baseHeight = 20 + Math.sin(position * Math.PI * 4) * 15;
    const variation = 16 + ((index * 17) % 37);
    const envelope = Math.sin(position * Math.PI) * 0.8 + 0.2;
    return (baseHeight + variation) * envelope;
  });
}
