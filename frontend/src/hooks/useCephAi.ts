"use client";
import { useMutation, useQuery } from "@tanstack/react-query";
import api from "@/lib/api";
import type { CephLandmark } from "@/types/ceph";

interface AiDetectParams {
  imageFile?: File;
  imageUrl?: string;
  imageWidth: number;
  imageHeight: number;
  analysisId?: string;
}

interface AiDetectResult {
  landmarks: CephLandmark[];
  method: "onnx" | "vlm" | "template";
}

export function useAiDetectLandmarks() {
  return useMutation({
    mutationFn: async (params: AiDetectParams): Promise<AiDetectResult> => {
      // Try ONNX backend first (the .NET backend at /api/ai/detect-landmarks)
      if (params.imageFile) {
        try {
          const formData = new FormData();
          formData.append("image", params.imageFile);
          formData.append("imageWidth", String(params.imageWidth));
          formData.append("imageHeight", String(params.imageHeight));

          const { data } = await api.post(
            "/api/ai/detect-landmarks",
            formData,
            {
              headers: { "Content-Type": "multipart/form-data" },
              timeout: 30000,
            }
          );
          return { landmarks: data.landmarks, method: "onnx" };
        } catch {
          // Fall through to VLM
        }
      }

      // Try VLM fallback (Next.js API route — must use fetch() not api axios,
      // because api.baseURL points to the .NET backend which already failed)
      try {
        // If we have an image URL, we need to convert it to base64 for the VLM route
        let imageBase64 = params.imageUrl ?? "";
        if (params.imageUrl && !params.imageUrl.startsWith("data:")) {
          try {
            const resp = await fetch(params.imageUrl);
            const blob = await resp.blob();
            imageBase64 = await new Promise<string>((resolve) => {
              const reader = new FileReader();
              reader.onloadend = () => resolve(reader.result as string);
              reader.readAsDataURL(blob);
            });
          } catch {
            // If we can't fetch the image, just pass the URL
          }
        }

        const vlmResp = await fetch("/api/ai/detect-landmarks", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            imageBase64,
            imageWidth: params.imageWidth,
            imageHeight: params.imageHeight,
          }),
          signal: AbortSignal.timeout(60000),
        });

        if (vlmResp.ok) {
          const vlmData = await vlmResp.json();
          if (vlmData.landmarks && vlmData.landmarks.length > 0) {
            return { landmarks: vlmData.landmarks, method: "vlm" };
          }
        }
      } catch {
        // Fall through to template
      }

      // Last resort: template-based from backend simulate endpoint
      if (params.analysisId) {
        const { data } = await api.post(
          `/api/ceph/${params.analysisId}/simulate`,
          {
            imageWidth: params.imageWidth,
            imageHeight: params.imageHeight,
            pixelsPerMm: 0,
          }
        );
        return { landmarks: data, method: "template" };
      }

      throw new Error("فشل كشف النقاط — لا يوجد نموذج AI متاح");
    },
  });
}

export function useAiModelStatus() {
  return useQuery({
    queryKey: ["ai-model-status"],
    queryFn: async () => {
      const { data } = await api.get("/api/ai/model-status");
      return data as {
        isModelLoaded: boolean;
        modelVersion?: string;
        modelType?: string;
      };
    },
    staleTime: 60_000,
  });
}
