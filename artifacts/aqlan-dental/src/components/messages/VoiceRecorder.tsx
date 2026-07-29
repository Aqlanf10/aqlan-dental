import { useState, useRef, useEffect } from "react";
import { Mic, Square, X } from "lucide-react";
import { cn } from "@/lib/utils";

interface VoiceRecorderProps {
  onRecorded: (blob: Blob, mimeType: string, duration: number) => void;
  disabled?: boolean;
}

export function VoiceRecorder({ onRecorded, disabled }: VoiceRecorderProps) {
  const [isRecording, setIsRecording] = useState(false);
  const [seconds, setSeconds] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const streamRef = useRef<MediaStream | null>(null);

  useEffect(() => {
    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
      streamRef.current?.getTracks().forEach((t) => t.stop());
    };
  }, []);

  const startRecording = async () => {
    setError(null);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;

      const mimeType = MediaRecorder.isTypeSupported("audio/webm;codecs=opus")
        ? "audio/webm;codecs=opus"
        : MediaRecorder.isTypeSupported("audio/ogg;codecs=opus")
          ? "audio/ogg;codecs=opus"
          : "audio/mp4";

      const recorder = new MediaRecorder(stream, { mimeType });
      chunksRef.current = [];
      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunksRef.current.push(e.data);
      };
      recorder.onstop = () => {
        const baseMime = mimeType.split(";")[0];
        const blob = new Blob(chunksRef.current, { type: baseMime });
        onRecorded(blob, baseMime, seconds);
        stream.getTracks().forEach((t) => t.stop());
        streamRef.current = null;
      };

      mediaRecorderRef.current = recorder;
      recorder.start(100);
      setIsRecording(true);
      setSeconds(0);
      timerRef.current = setInterval(() => setSeconds((s) => s + 1), 1000);
    } catch {
      setError("تعذّر الوصول إلى الميكروفون");
    }
  };

  const stopRecording = () => {
    if (timerRef.current) clearInterval(timerRef.current);
    mediaRecorderRef.current?.stop();
    setIsRecording(false);
    setSeconds(0);
  };

  const cancelRecording = () => {
    if (timerRef.current) clearInterval(timerRef.current);
    if (mediaRecorderRef.current?.state === "recording") {
      mediaRecorderRef.current.ondataavailable = null;
      mediaRecorderRef.current.onstop = null;
      mediaRecorderRef.current.stop();
    }
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    setIsRecording(false);
    setSeconds(0);
    setError(null);
  };

  const formatTime = (s: number) =>
    `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;

  if (isRecording) {
    return (
      <div className="flex items-center gap-2 flex-shrink-0">
        <span className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
        <span className="text-xs text-red-600 font-mono tabular-nums" dir="ltr">
          {formatTime(seconds)}
        </span>
        <button
          type="button"
          onClick={cancelRecording}
          className="w-8 h-8 rounded-lg bg-gray-100 hover:bg-gray-200 flex items-center justify-center text-gray-500 transition"
          title="إلغاء التسجيل"
        >
          <X className="w-3.5 h-3.5" />
        </button>
        <button
          type="button"
          onClick={stopRecording}
          className="w-8 h-8 rounded-lg bg-red-100 hover:bg-red-200 flex items-center justify-center text-red-600 transition"
          title="إيقاف وإرسال"
        >
          <Square className="w-3.5 h-3.5 fill-red-600" />
        </button>
        {error && <span className="text-[10px] text-red-500">{error}</span>}
      </div>
    );
  }

  return (
    <button
      type="button"
      onClick={startRecording}
      disabled={disabled}
      data-testid="voice-recorder-button"
      aria-label="تسجيل رسالة صوتية"
      className={cn(
        "min-w-10 w-10 h-10 rounded-lg flex items-center justify-center transition flex-shrink-0 relative z-10",
        disabled
          ? "bg-gray-100 text-gray-300 cursor-not-allowed"
          : "bg-gray-100 text-gray-500 hover:bg-gray-200 hover:text-gray-700"
      )}
      title="تسجيل رسالة صوتية"
    >
      <Mic className="w-4 h-4" />
    </button>
  );
}
