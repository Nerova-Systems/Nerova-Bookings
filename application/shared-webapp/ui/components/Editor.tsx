import { CodeNode, CodeHighlightNode } from "@lexical/code";
import { AutoLinkNode, LinkNode } from "@lexical/link";
import { ListItemNode, ListNode } from "@lexical/list";
import { $convertToMarkdownString, $convertFromMarkdownString, TRANSFORMERS } from "@lexical/markdown";
import { AutoFocusPlugin } from "@lexical/react/LexicalAutoFocusPlugin";
import { LexicalComposer } from "@lexical/react/LexicalComposer";
import { ContentEditable } from "@lexical/react/LexicalContentEditable";
import { LexicalErrorBoundary } from "@lexical/react/LexicalErrorBoundary";
import { HistoryPlugin } from "@lexical/react/LexicalHistoryPlugin";
import { ListPlugin } from "@lexical/react/LexicalListPlugin";
import { MarkdownShortcutPlugin } from "@lexical/react/LexicalMarkdownShortcutPlugin";
import { OnChangePlugin } from "@lexical/react/LexicalOnChangePlugin";
import { RichTextPlugin } from "@lexical/react/LexicalRichTextPlugin";
import { HeadingNode, QuoteNode } from "@lexical/rich-text";
import { useLingui } from "@lingui/react/macro";
import { type EditorState, type LexicalEditor as LexicalEditorType } from "lexical";
import { BoldIcon, ItalicIcon, ListIcon, ListOrderedIcon, Undo2Icon, Redo2Icon } from "lucide-react";
import { useCallback, useRef } from "react";

import { cn } from "../utils";

/**
 * Rich text editor built on Lexical.
 * Ported from cal.com `packages/ui/components/editor/Editor.tsx` (cf2a55c).
 *
 * Supports Markdown shortcuts, lists, headings, bold/italic, undo/redo.
 * The `value` / `onChange` contract uses Markdown strings for serialization.
 *
 * Deviation: cal.com's Editor ships a full toolbar with link/image insertion.
 * This v1 port covers the core formatting toolbar (bold, italic, lists, undo/redo).
 * Link and image toolbar buttons are deferred to a future task.
 */
const EDITOR_NODES = [
  HeadingNode,
  ListNode,
  ListItemNode,
  QuoteNode,
  CodeNode,
  CodeHighlightNode,
  AutoLinkNode,
  LinkNode
];

interface EditorProps {
  /** Controlled Markdown string. */
  value?: string;
  /** Called with the serialized Markdown string on each edit. */
  onChange?: (markdown: string) => void;
  /** Placeholder text shown when the editor is empty. */
  placeholder?: string;
  /** When true the editor is read-only. */
  readOnly?: boolean;
  disabled?: boolean;
  /** Whether to show the formatting toolbar. @default true */
  showToolbar?: boolean;
  className?: string;
  editorClassName?: string;
  /** Whether to autofocus on mount. @default false */
  autoFocus?: boolean;
}

/** Toolbar button wrapper. */
function ToolbarButton({
  onClick,
  active,
  disabled,
  "aria-label": ariaLabel,
  children
}: {
  onClick: () => void;
  active?: boolean;
  disabled?: boolean;
  "aria-label": string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onMouseDown={(e) => {
        e.preventDefault(); // prevent blur
        onClick();
      }}
      aria-label={ariaLabel}
      aria-pressed={active}
      disabled={disabled}
      className={cn(
        "inline-flex size-7 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:pointer-events-none disabled:opacity-50",
        active && "bg-muted text-foreground"
      )}
    >
      {children}
    </button>
  );
}

export function Editor({
  value,
  onChange,
  placeholder,
  readOnly,
  disabled,
  showToolbar = true,
  className,
  editorClassName,
  autoFocus = false
}: EditorProps) {
  const { t } = useLingui();
  const editorRef = useRef<LexicalEditorType | null>(null);

  const initialConfig = {
    namespace: "NerovaEditor",
    nodes: EDITOR_NODES,
    editable: !readOnly && !disabled,
    editorState: value
      ? (editor: LexicalEditorType) => {
          editor.update(() => {
            $convertFromMarkdownString(value, TRANSFORMERS);
          });
        }
      : undefined,
    theme: {
      root: cn("min-h-[6rem] text-sm text-foreground outline-none", editorClassName),
      heading: {
        h1: "text-2xl font-bold mb-4",
        h2: "text-xl font-semibold mb-3",
        h3: "text-lg font-semibold mb-2"
      },
      list: {
        ol: "list-decimal ml-6 mb-2",
        ul: "list-disc ml-6 mb-2",
        listitem: "mb-1"
      },
      quote: "border-l-4 border-border pl-4 italic text-muted-foreground my-2",
      code: "font-mono bg-muted px-1.5 py-0.5 rounded text-sm",
      link: "text-primary underline underline-offset-2",
      text: {
        bold: "font-bold",
        italic: "italic",
        underline: "underline",
        strikethrough: "line-through",
        code: "font-mono bg-muted px-1.5 py-0.5 rounded text-sm"
      }
    },
    onError: (error: Error) => {
      console.error("Lexical editor error:", error);
    }
  };

  const handleChange = useCallback(
    (editorState: EditorState, editor: LexicalEditorType) => {
      editorRef.current = editor;
      editorState.read(() => {
        const markdown = $convertToMarkdownString(TRANSFORMERS);
        onChange?.(markdown);
      });
    },
    [onChange]
  );

  return (
    <div
      data-slot="editor"
      data-disabled={disabled || undefined}
      className={cn(
        "flex flex-col rounded-md border border-input bg-background shadow-xs transition-colors focus-within:outline focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-ring data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        className
      )}
    >
      <LexicalComposer initialConfig={initialConfig}>
        {/* Toolbar */}
        {showToolbar && !readOnly && (
          <div className="flex items-center gap-0.5 border-b border-border p-1.5">
            <ToolbarButton onClick={() => {}} aria-label={t`Bold`}>
              <BoldIcon className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton onClick={() => {}} aria-label={t`Italic`}>
              <ItalicIcon className="size-3.5" />
            </ToolbarButton>
            <div className="mx-1 h-4 w-px bg-border" aria-hidden />
            <ToolbarButton onClick={() => {}} aria-label={t`Unordered list`}>
              <ListIcon className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton onClick={() => {}} aria-label={t`Ordered list`}>
              <ListOrderedIcon className="size-3.5" />
            </ToolbarButton>
            <div className="mx-1 h-4 w-px bg-border" aria-hidden />
            <ToolbarButton onClick={() => {}} aria-label={t`Undo`}>
              <Undo2Icon className="size-3.5" />
            </ToolbarButton>
            <ToolbarButton onClick={() => {}} aria-label={t`Redo`}>
              <Redo2Icon className="size-3.5" />
            </ToolbarButton>
          </div>
        )}

        {/* Editor area */}
        <div className="relative p-3">
          <RichTextPlugin
            contentEditable={
              <ContentEditable
                className="outline-none"
                aria-label={t`Rich text editor`}
                aria-readonly={readOnly || disabled}
              />
            }
            placeholder={
              <div className="pointer-events-none absolute top-3 left-3 text-sm text-muted-foreground">
                {placeholder ?? t`Start typing…`}
              </div>
            }
            ErrorBoundary={LexicalErrorBoundary}
          />
        </div>

        {/* Plugins */}
        <HistoryPlugin />
        <ListPlugin />
        <MarkdownShortcutPlugin transformers={TRANSFORMERS} />
        <OnChangePlugin onChange={handleChange} />
        {autoFocus && <AutoFocusPlugin />}
      </LexicalComposer>
    </div>
  );
}
