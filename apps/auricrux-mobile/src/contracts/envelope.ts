export type ThinkingMode = "quick" | "deep" | "auto";
export type SearchScope = "internal" | "public" | "both";
export type FeedbackRating = "up" | "down";

export type AppContext = {
  source: "auricrux-mobile";
  route: "/construction";
  specialistAgent: "construction-expert";
  thinkingMode: ThinkingMode;
  searchScope: SearchScope;
  appRelease: string;
  sessionId: string;
  traceId: string;
  turnId: string;
};

export type ChatRequestEnvelope = {
  message: string;
  route: "/construction";
  context: AppContext & {
    voiceTarget: "construction-expert";
    tone: "professional-practical";
  };
};

export type FeedbackRequestEnvelope = {
  rating: FeedbackRating;
  message: string;
  reply: string;
  route: "/construction";
  context: AppContext;
};

const randomToken = (): string => Math.random().toString(36).slice(2, 10);

export function createSessionId(): string {
  return `sess-${Date.now()}-${randomToken()}`;
}

export function createTraceId(): string {
  return `trace-${Date.now()}-${randomToken()}`;
}

export function createTurnId(turnNumber: number): string {
  return `turn-${String(turnNumber).padStart(5, "0")}`;
}

type BaseContextInput = {
  thinkingMode: ThinkingMode;
  searchScope: SearchScope;
  appRelease: string;
  sessionId: string;
  traceId: string;
  turnId: string;
};

function buildBaseContext(input: BaseContextInput): AppContext {
  return {
    source: "auricrux-mobile",
    route: "/construction",
    specialistAgent: "construction-expert",
    thinkingMode: input.thinkingMode,
    searchScope: input.searchScope,
    appRelease: input.appRelease,
    sessionId: input.sessionId,
    traceId: input.traceId,
    turnId: input.turnId,
  };
}

type BuildChatRequestInput = BaseContextInput & {
  message: string;
};

export function buildChatRequestEnvelope(input: BuildChatRequestInput): ChatRequestEnvelope {
  return {
    message: input.message,
    route: "/construction",
    context: {
      ...buildBaseContext(input),
      voiceTarget: "construction-expert",
      tone: "professional-practical",
    },
  };
}

type BuildFeedbackInput = BaseContextInput & {
  rating: FeedbackRating;
  message: string;
  reply: string;
};

export function buildFeedbackRequestEnvelope(input: BuildFeedbackInput): FeedbackRequestEnvelope {
  return {
    rating: input.rating,
    message: input.message,
    reply: input.reply,
    route: "/construction",
    context: buildBaseContext(input),
  };
}

export function assertValidFeedbackEnvelope(payload: FeedbackRequestEnvelope): void {
  if (!payload.message.trim()) {
    throw new Error("Feedback payload is missing message.");
  }
  if (!payload.reply.trim()) {
    throw new Error("Feedback payload is missing reply.");
  }
  if (!payload.context.traceId.trim() || !payload.context.sessionId.trim()) {
    throw new Error("Feedback payload is missing trace or session identifiers.");
  }
}
