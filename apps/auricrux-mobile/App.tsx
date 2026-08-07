import React, { Component, useEffect, useMemo, useRef, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { StatusBar } from "expo-status-bar";
import Constants from "expo-constants";
import {
  type FeedbackRating,
  type SearchScope,
  type ThinkingMode,
  assertValidFeedbackEnvelope,
  buildChatRequestEnvelope,
  buildFeedbackRequestEnvelope,
  createSessionId,
  createTraceId,
  createTurnId,
} from "./src/contracts/envelope";

let Speech: { speak: (t: string, o?: object) => void; stop: () => void } | null = null;

type Role = "user" | "assistant";
type ExpertMode = "estimator" | "field-ops" | "safety" | "project-manager" | "closeout";

type ChatMessage = {
  id: string;
  role: Role;
  text: string;
  createdAt: string;
  traceId?: string;
  citations?: Citation[];
};

type Citation = {
  label: string;
  detail: string;
};

type AuricruxResponse = {
  ok?: boolean;
  reply?: string;
  error?: string;
  sources?: Array<{ title?: string; type?: string; url?: string }>;
};

const API_BASE_URL = Constants.expoConfig?.extra?.API_BASE_URL ?? "https://auricrux-central.azurewebsites.net";
const RELEASE_LABEL = Constants.expoConfig?.extra?.RELEASE_TAG ?? "unversioned";
const APP_AUTH_KEY = Constants.expoConfig?.extra?.AURICRUX_APP_KEY ?? "";
const API_HOST_LABEL = API_BASE_URL.replace(/^https?:\/\//, "");

const EXPERT_MODE_TITLES: Record<ExpertMode, string> = {
  estimator: "Estimator",
  "field-ops": "Field Ops",
  safety: "Safety",
  "project-manager": "Project Manager",
  closeout: "Closeout",
};

const MODE_PROMPTS: Record<ExpertMode, string[]> = {
  estimator: [
    "Build a bid-ready quantity takeoff workflow for concrete and reinforcing.",
    "Create labor and material estimate assumptions I can defend in review.",
    "Show a risk-adjusted estimate checklist for scope gaps and alternates.",
  ],
  "field-ops": [
    "Draft a daily work plan with crew sequencing and handoff checks.",
    "Create a pre-task plan for two-trade coordination in a tight area.",
    "Generate a field issue escalation playbook with accountable owners.",
  ],
  safety: [
    "Create a hazard analysis for roof edge work with weather risk factors.",
    "Give a toolbox talk script for trenching and excavation controls.",
    "Build a stop-work trigger checklist for high-risk activities.",
  ],
  "project-manager": [
    "Draft a weekly project control review format for schedule, cost, and risk.",
    "Create an RFI and submittal recovery plan to protect critical path.",
    "Build owner update talking points for budget pressure scenarios.",
  ],
  closeout: [
    "Create a closeout turnover checklist with evidence and sign-off gates.",
    "Build a warranty handoff package template for owner operations.",
    "Generate punchlist burn-down rules with quality acceptance criteria.",
  ],
};

const nowIso = () => new Date().toISOString();

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

function deriveCitations(response: AuricruxResponse, scope: SearchScope): Citation[] {
  if (Array.isArray(response.sources) && response.sources.length > 0) {
    return response.sources.slice(0, 3).map((item, index) => ({
      label: item.title?.trim() || `Source ${index + 1}`,
      detail: item.url?.trim() || item.type?.trim() || "Backend source",
    }));
  }

  if (scope === "internal") {
    return [{ label: "Internal Construction Knowledge", detail: "Grounded on Auricrux internal knowledge scope" }];
  }
  if (scope === "public") {
    return [{ label: "Public Construction Guidance", detail: "Grounded on public standards and references" }];
  }
  return [{ label: "Hybrid Grounding", detail: "Internal and public construction knowledge blend" }];
}

type ErrorBoundaryState = { hasError: boolean; message: string };

class AppErrorBoundary extends Component<{ children: React.ReactNode }, ErrorBoundaryState> {
  constructor(props: { children: React.ReactNode }) {
    super(props);
    this.state = { hasError: false, message: "" };
  }

  static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
    const message = error instanceof Error ? error.message : String(error);
    return { hasError: true, message };
  }

  render() {
    if (this.state.hasError) {
      return (
        <View style={styles.errorRoot}>
          <Text style={styles.errorTitle}>Auricrux</Text>
          <Text style={styles.errorSubtitle}>Startup issue detected.</Text>
          <Text style={styles.errorMessage}>{this.state.message}</Text>
        </View>
      );
    }
    return this.props.children;
  }
}

export default function App() {
  const [expertMode, setExpertMode] = useState<ExpertMode>("project-manager");
  const [thinkingMode, setThinkingMode] = useState<ThinkingMode>("auto");
  const [searchScope, setSearchScope] = useState<SearchScope>("both");
  const [projectName, setProjectName] = useState("");
  const [projectPhase, setProjectPhase] = useState("");
  const [regionCode, setRegionCode] = useState("");
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [assistantTyping, setAssistantTyping] = useState(false);
  const [autoSpeak, setAutoSpeak] = useState(false);
  const [seedCount, setSeedCount] = useState(0);
  const [memoryLedger, setMemoryLedger] = useState<string[]>([]);
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: "m0",
      role: "assistant",
      createdAt: nowIso(),
      text:
        "Auricrux Construction Expert is online. I will answer as a construction specialist with practical execution guidance, safety awareness, and defensible decision support.",
    },
  ]);

  const sessionIdRef = useRef(createSessionId());
  const turnCounterRef = useRef(0);
  const lastPairRef = useRef<{ message: string; reply: string; traceId: string; turnId: string } | null>(null);

  useEffect(() => {
    if (!Speech) {
      try {
        Speech = require("expo-speech");
      } catch {
        // Speech is optional.
      }
    }
  }, []);

  const quickPrompts = useMemo(() => MODE_PROMPTS[expertMode], [expertMode]);

  const append = (message: ChatMessage) => {
    setMessages((prev) => [...prev, message]);
  };

  const updateMemoryLedger = (userMessage: string, assistantReply: string) => {
    const userKey = userMessage.trim();
    const replyKey = assistantReply.trim();
    const seeds = [
      userKey ? `Need: ${userKey.slice(0, 84)}` : "",
      replyKey ? `Guidance: ${replyKey.slice(0, 96)}` : "",
      projectName.trim() ? `Project: ${projectName.trim()}` : "",
    ].filter(Boolean);

    setMemoryLedger((prev) => {
      const combined = [...seeds, ...prev].filter(Boolean);
      const deduped: string[] = [];
      for (const item of combined) {
        if (!deduped.includes(item)) {
          deduped.push(item);
        }
      }
      return deduped.slice(0, 6);
    });
  };

  const streamAssistantReply = async (reply: string, traceId: string, citations: Citation[]) => {
    const messageId = `a-${Date.now()}`;
    append({ id: messageId, role: "assistant", text: "", createdAt: nowIso(), traceId, citations });

    const chunkSize = 42;
    for (let i = 0; i < reply.length; i += chunkSize) {
      const next = reply.slice(0, i + chunkSize);
      setMessages((prev) => prev.map((m) => (m.id === messageId ? { ...m, text: next } : m)));
      await wait(20);
    }
  };

  const speak = (text: string) => {
    if (!Speech) return;
    try {
      Speech.stop();
      Speech.speak(text, {
        language: "en-US",
        pitch: 1.0,
        rate: 0.95,
      });
    } catch {
      // Ignore speech failures.
    }
  };

  const postJson = async (path: string, body: Record<string, unknown>, traceId: string) => {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 30000);
    try {
      const headers: Record<string, string> = {
        "Content-Type": "application/json",
        Accept: "application/json",
        "x-auricrux-source": "auricrux-mobile",
        "x-auricrux-session-id": sessionIdRef.current,
        "x-trace-id": traceId,
      };
      if (APP_AUTH_KEY) {
        headers["x-auricrux-key"] = APP_AUTH_KEY;
      }

      const res = await fetch(`${API_BASE_URL}${path}`, {
        method: "POST",
        headers,
        body: JSON.stringify(body),
        signal: controller.signal,
      });

      const raw = await res.text();
      let parsed: AuricruxResponse | null = null;
      if (raw.trim().length > 0) {
        try {
          parsed = JSON.parse(raw) as AuricruxResponse;
        } catch {
          throw new Error(`Backend returned non-JSON payload (HTTP ${res.status}).`);
        }
      }

      if (!res.ok) {
        throw new Error(parsed?.error ?? `Backend request failed (HTTP ${res.status}).`);
      }
      if (!parsed) {
        throw new Error("Backend returned an empty response.");
      }

      return parsed;
    } catch (error) {
      if (error instanceof Error && error.name === "AbortError") {
        throw new Error("Backend request timed out after 30 seconds.");
      }
      if (error instanceof Error) {
        throw error;
      }
      throw new Error(String(error));
    } finally {
      clearTimeout(timeoutId);
    }
  };

  const sendMessage = async (messageOverride?: string) => {
    const trimmed = (messageOverride ?? input).trim();
    if (!trimmed || busy) {
      return;
    }

    append({ id: `u-${Date.now()}`, role: "user", text: trimmed, createdAt: nowIso() });
    setInput("");
    setBusy(true);
    setAssistantTyping(true);

    try {
      turnCounterRef.current += 1;
      const traceId = createTraceId();
      const turnId = createTurnId(turnCounterRef.current);

      const requestPayload = buildChatRequestEnvelope({
        message: trimmed,
        thinkingMode,
        searchScope,
        appRelease: RELEASE_LABEL,
        sessionId: sessionIdRef.current,
        traceId,
        turnId,
      });

      const requestBody = {
        ...requestPayload,
        context: {
          ...requestPayload.context,
          expertMode,
          projectContext: {
            projectName: projectName.trim(),
            projectPhase: projectPhase.trim(),
            regionCode: regionCode.trim(),
          },
        },
      } as unknown as Record<string, unknown>;

      const data = await postJson("/api/auricrux", requestBody, traceId);
      if (!data.ok || !data.reply) {
        throw new Error(data.error || "No reply was returned from Auricrux.");
      }

      const citations = deriveCitations(data, searchScope);
      await streamAssistantReply(data.reply, traceId, citations);
      lastPairRef.current = { message: trimmed, reply: data.reply, traceId, turnId };
      updateMemoryLedger(trimmed, data.reply);
      if (autoSpeak) {
        speak(data.reply);
      }
    } catch (error) {
      const text = error instanceof Error ? error.message : "Unable to reach backend.";
      append({ id: `e-${Date.now()}`, role: "assistant", text: `Connection issue: ${text}`, createdAt: nowIso() });
    } finally {
      setAssistantTyping(false);
      setBusy(false);
    }
  };

  const sendFeedback = async (rating: FeedbackRating) => {
    const pair = lastPairRef.current;
    if (!pair) {
      Alert.alert("No response yet", "Send a message first so feedback can be attached.");
      return;
    }

    try {
      const payload = buildFeedbackRequestEnvelope({
        rating,
        message: pair.message,
        reply: pair.reply,
        thinkingMode,
        searchScope,
        appRelease: RELEASE_LABEL,
        sessionId: sessionIdRef.current,
        traceId: pair.traceId,
        turnId: pair.turnId,
      });

      assertValidFeedbackEnvelope(payload);

      const feedbackBody = {
        ...payload,
        context: {
          ...payload.context,
          expertMode,
          projectContext: {
            projectName: projectName.trim(),
            projectPhase: projectPhase.trim(),
            regionCode: regionCode.trim(),
          },
        },
      } as unknown as Record<string, unknown>;

      const data = await postJson("/api/auricrux", feedbackBody, pair.traceId);
      if (!data.ok) {
        throw new Error(data.error || "Feedback not accepted");
      }

      if (rating === "up") {
        setSeedCount((prev) => prev + 1);
      }

      Alert.alert("Feedback recorded", `Saved: ${rating.toUpperCase()}`);
    } catch (error) {
      const text = error instanceof Error ? error.message : "Feedback failed";
      Alert.alert("Feedback failed", text);
    }
  };

  const regenerateLast = async () => {
    const lastUser = [...messages].reverse().find((m) => m.role === "user");
    if (!lastUser) {
      Alert.alert("Nothing to regenerate", "Send at least one message first.");
      return;
    }
    await sendMessage(lastUser.text);
  };

  const startVoiceInput = async () => {
    Alert.alert("Voice input", "Push-to-talk capture will be enabled in the next release lane.");
  };

  return (
    <AppErrorBoundary>
      <View style={styles.root}>
        <StatusBar style="dark" />
        <View style={styles.bgGradientOne} />
        <View style={styles.bgGradientTwo} />

        <KeyboardAvoidingView style={styles.container} behavior={Platform.OS === "ios" ? "padding" : undefined}>
          <View style={styles.headerCard}>
            <Text style={styles.title}>Auricrux</Text>
            <Text style={styles.subtitle}>Construction Intelligence Operator</Text>
            <View style={styles.statusRow}>
              <View style={styles.statusDot} />
              <Text style={styles.statusText}>Online {API_HOST_LABEL}</Text>
              <Text style={styles.statusPill}>Release {RELEASE_LABEL}</Text>
              <Text style={styles.statusPill}>Seed {seedCount}</Text>
            </View>
          </View>

          <View style={styles.panelCard}>
            <Text style={styles.panelTitle}>Expert Workflow</Text>
            <View style={styles.chipsRow}>
              {(Object.keys(EXPERT_MODE_TITLES) as ExpertMode[]).map((mode) => (
                <Pressable
                  key={mode}
                  style={[styles.chip, expertMode === mode && styles.chipActive]}
                  onPress={() => setExpertMode(mode)}
                >
                  <Text style={[styles.chipText, expertMode === mode && styles.chipTextActive]}>{EXPERT_MODE_TITLES[mode]}</Text>
                </Pressable>
              ))}
            </View>

            <Text style={styles.panelTitle}>Thinking Mode</Text>
            <View style={styles.chipsRow}>
              {(["quick", "auto", "deep"] as ThinkingMode[]).map((mode) => (
                <Pressable
                  key={mode}
                  style={[styles.chip, thinkingMode === mode && styles.chipActive]}
                  onPress={() => setThinkingMode(mode)}
                >
                  <Text style={[styles.chipText, thinkingMode === mode && styles.chipTextActive]}>{mode}</Text>
                </Pressable>
              ))}
            </View>

            <Text style={styles.panelTitle}>Search Scope</Text>
            <View style={styles.chipsRow}>
              {(["internal", "public", "both"] as SearchScope[]).map((scope) => (
                <Pressable
                  key={scope}
                  style={[styles.chip, searchScope === scope && styles.chipActive]}
                  onPress={() => setSearchScope(scope)}
                >
                  <Text style={[styles.chipText, searchScope === scope && styles.chipTextActive]}>{scope}</Text>
                </Pressable>
              ))}
            </View>

            <Text style={styles.panelTitle}>Project Context</Text>
            <View style={styles.contextRow}>
              <TextInput
                style={styles.contextInput}
                value={projectName}
                onChangeText={setProjectName}
                placeholder="Project name"
              />
              <TextInput
                style={styles.contextInput}
                value={projectPhase}
                onChangeText={setProjectPhase}
                placeholder="Phase"
              />
              <TextInput
                style={styles.contextInput}
                value={regionCode}
                onChangeText={setRegionCode}
                placeholder="Region"
              />
            </View>

            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.promptRow}>
              {quickPrompts.map((prompt) => (
                <Pressable key={prompt} style={styles.promptChip} onPress={() => setInput(prompt)}>
                  <Text style={styles.promptText}>{prompt}</Text>
                </Pressable>
              ))}
            </ScrollView>

            <Text style={styles.panelTitle}>Memory Ledger</Text>
            <View style={styles.ledgerBox}>
              {memoryLedger.length === 0 ? (
                <Text style={styles.ledgerEmpty}>No retained decisions yet. Send a message to build project memory.</Text>
              ) : (
                memoryLedger.map((item) => (
                  <Text key={item} style={styles.ledgerItem}>
                    • {item}
                  </Text>
                ))
              )}
            </View>
          </View>

          <ScrollView style={styles.thread} contentContainerStyle={styles.threadBody}>
            {messages.map((m) => (
              <View key={m.id} style={[styles.bubble, m.role === "user" ? styles.bubbleUser : styles.bubbleAssistant]}>
                <View style={styles.bubbleMetaRow}>
                  <Text style={styles.bubbleRole}>{m.role === "user" ? "You" : "Auricrux"}</Text>
                  <Text style={styles.bubbleTime}>{new Date(m.createdAt).toLocaleTimeString()}</Text>
                </View>
                <Text style={styles.bubbleText}>{m.text}</Text>
                {m.role === "assistant" && m.citations && m.citations.length > 0 ? (
                  <View style={styles.citationWrap}>
                    {m.citations.map((citation) => (
                      <View key={`${m.id}-${citation.label}-${citation.detail}`} style={styles.citationCard}>
                        <Text style={styles.citationLabel}>{citation.label}</Text>
                        <Text style={styles.citationDetail}>{citation.detail}</Text>
                      </View>
                    ))}
                  </View>
                ) : null}
              </View>
            ))}
            {assistantTyping ? <Text style={styles.typingText}>Auricrux is composing...</Text> : null}
            {busy ? <ActivityIndicator color="#0B5A4A" size="small" /> : null}
          </ScrollView>

          <View style={styles.actionsRow}>
            <Pressable style={styles.actionBtn} onPress={() => sendFeedback("up")}>
              <Text style={styles.actionText}>Helpful</Text>
            </Pressable>
            <Pressable style={styles.actionBtn} onPress={() => sendFeedback("down")}>
              <Text style={styles.actionText}>Needs Work</Text>
            </Pressable>
            <Pressable style={styles.actionBtn} onPress={regenerateLast}>
              <Text style={styles.actionText}>Regenerate</Text>
            </Pressable>
            <Pressable style={styles.actionBtn} onPress={startVoiceInput}>
              <Text style={styles.actionText}>Voice</Text>
            </Pressable>
            <Pressable style={[styles.actionBtn, autoSpeak ? styles.actionBtnLive : null]} onPress={() => setAutoSpeak((prev) => !prev)}>
              <Text style={styles.actionText}>{autoSpeak ? "Speak On" : "Speak Off"}</Text>
            </Pressable>
          </View>

          <View style={styles.inputRow}>
            <TextInput
              value={input}
              onChangeText={setInput}
              placeholder="Ask for a construction-ready plan, estimate, safety check, or recovery strategy..."
              style={styles.chatInput}
              multiline
              editable={!busy}
            />
            <Pressable style={styles.sendBtn} onPress={() => sendMessage()} disabled={busy}>
              <Text style={styles.sendBtnText}>{busy ? "..." : "Send"}</Text>
            </Pressable>
          </View>
        </KeyboardAvoidingView>
      </View>
    </AppErrorBoundary>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: "#E8ECE9",
  },
  bgGradientOne: {
    position: "absolute",
    width: 340,
    height: 340,
    borderRadius: 999,
    backgroundColor: "#B3E0D4",
    top: -120,
    left: -100,
    opacity: 0.5,
  },
  bgGradientTwo: {
    position: "absolute",
    width: 360,
    height: 360,
    borderRadius: 999,
    backgroundColor: "#C6D7F5",
    bottom: -140,
    right: -120,
    opacity: 0.35,
  },
  container: {
    flex: 1,
    paddingTop: 46,
    paddingHorizontal: 14,
    paddingBottom: 12,
    gap: 8,
  },
  headerCard: {
    backgroundColor: "#FFFFFFE8",
    borderColor: "#D1DADF",
    borderWidth: 1,
    borderRadius: 14,
    padding: 12,
  },
  title: {
    fontSize: 30,
    fontWeight: "800",
    color: "#102033",
  },
  subtitle: {
    fontSize: 13,
    fontWeight: "600",
    color: "#34465D",
    marginTop: 2,
  },
  statusRow: {
    marginTop: 8,
    flexDirection: "row",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 8,
  },
  statusDot: {
    width: 8,
    height: 8,
    borderRadius: 99,
    backgroundColor: "#16A34A",
  },
  statusText: {
    color: "#22364B",
    fontSize: 12,
    fontWeight: "700",
  },
  statusPill: {
    fontSize: 11,
    color: "#0F2740",
    backgroundColor: "#DDEAF9",
    borderColor: "#C4D7EE",
    borderWidth: 1,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 999,
    fontWeight: "700",
  },
  panelCard: {
    backgroundColor: "#FFFFFFD9",
    borderRadius: 14,
    borderWidth: 1,
    borderColor: "#D3DBE2",
    padding: 10,
    gap: 6,
  },
  panelTitle: {
    color: "#11273D",
    fontSize: 11,
    fontWeight: "800",
    textTransform: "uppercase",
    letterSpacing: 0.4,
  },
  chipsRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 7,
  },
  chip: {
    backgroundColor: "#F4F7FB",
    borderColor: "#D0D8E2",
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 10,
    paddingVertical: 6,
  },
  chipActive: {
    backgroundColor: "#0F346A",
    borderColor: "#0F346A",
  },
  chipText: {
    color: "#2A3C52",
    fontSize: 12,
    fontWeight: "700",
  },
  chipTextActive: {
    color: "#FFFFFF",
  },
  contextRow: {
    flexDirection: "row",
    gap: 6,
  },
  contextInput: {
    flex: 1,
    backgroundColor: "#FFFFFF",
    borderColor: "#CBD5E1",
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 8,
    paddingVertical: 7,
    fontSize: 12,
    color: "#12253A",
  },
  promptRow: {
    gap: 8,
    paddingTop: 4,
    paddingBottom: 4,
  },
  promptChip: {
    backgroundColor: "#EEF4FF",
    borderRadius: 11,
    borderColor: "#D2E0F4",
    borderWidth: 1,
    paddingHorizontal: 10,
    paddingVertical: 8,
    maxWidth: 300,
  },
  promptText: {
    color: "#244264",
    fontSize: 12,
    fontWeight: "600",
  },
  ledgerBox: {
    backgroundColor: "#F4F8FF",
    borderColor: "#D7E3F4",
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 9,
    paddingVertical: 8,
    gap: 4,
  },
  ledgerEmpty: {
    color: "#5B6F86",
    fontSize: 11,
    fontStyle: "italic",
  },
  ledgerItem: {
    color: "#1F3650",
    fontSize: 11,
    fontWeight: "600",
  },
  thread: {
    flex: 1,
    backgroundColor: "#FFFFFFCC",
    borderRadius: 14,
    borderWidth: 1,
    borderColor: "#D4DEE5",
  },
  threadBody: {
    padding: 10,
    gap: 9,
  },
  bubble: {
    borderRadius: 12,
    padding: 10,
  },
  bubbleUser: {
    alignSelf: "flex-end",
    maxWidth: "92%",
    backgroundColor: "#D8E8FF",
  },
  bubbleAssistant: {
    alignSelf: "flex-start",
    maxWidth: "96%",
    backgroundColor: "#F8FAFD",
    borderColor: "#D6DEE8",
    borderWidth: 1,
  },
  bubbleMetaRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 4,
  },
  bubbleRole: {
    color: "#304259",
    fontWeight: "700",
    fontSize: 12,
  },
  bubbleTime: {
    color: "#5E7085",
    fontWeight: "600",
    fontSize: 10,
  },
  bubbleText: {
    color: "#13273D",
    fontSize: 15,
    lineHeight: 21,
  },
  citationWrap: {
    marginTop: 8,
    gap: 6,
  },
  citationCard: {
    backgroundColor: "#EFF5FF",
    borderColor: "#CEDCF1",
    borderWidth: 1,
    borderRadius: 9,
    paddingHorizontal: 8,
    paddingVertical: 7,
  },
  citationLabel: {
    color: "#1E3A5A",
    fontSize: 11,
    fontWeight: "800",
  },
  citationDetail: {
    marginTop: 2,
    color: "#3F5B79",
    fontSize: 11,
    fontWeight: "600",
  },
  typingText: {
    color: "#40566F",
    fontSize: 12,
    fontWeight: "600",
    fontStyle: "italic",
  },
  actionsRow: {
    flexDirection: "row",
    gap: 7,
  },
  actionBtn: {
    flex: 1,
    backgroundColor: "#2B3F5D",
    borderRadius: 9,
    paddingVertical: 10,
    alignItems: "center",
    justifyContent: "center",
  },
  actionBtnLive: {
    backgroundColor: "#0B6B75",
  },
  actionText: {
    color: "#FFFFFF",
    fontSize: 11,
    fontWeight: "800",
  },
  inputRow: {
    flexDirection: "row",
    alignItems: "flex-end",
    gap: 8,
  },
  chatInput: {
    flex: 1,
    minHeight: 52,
    maxHeight: 128,
    backgroundColor: "#FFFFFF",
    borderColor: "#C8D3DF",
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 9,
    color: "#12263C",
  },
  sendBtn: {
    minWidth: 84,
    borderRadius: 12,
    paddingVertical: 14,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#0F3D7A",
  },
  sendBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "800",
  },
  errorRoot: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#F3F4F6",
    padding: 24,
  },
  errorTitle: {
    fontSize: 24,
    color: "#132A45",
    fontWeight: "800",
  },
  errorSubtitle: {
    marginTop: 8,
    color: "#B91C1C",
    fontWeight: "700",
  },
  errorMessage: {
    marginTop: 8,
    color: "#334155",
    textAlign: "center",
  },
});
