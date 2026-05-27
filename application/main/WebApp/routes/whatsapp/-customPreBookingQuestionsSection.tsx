/* eslint-disable max-lines, max-lines-per-function */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Field, FieldError, FieldLabel } from "@repo/ui/components/Field";
import { Input } from "@repo/ui/components/Input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { PlusIcon, XIcon } from "lucide-react";
import { useState } from "react";

export type QuestionType = "Text" | "MultipleChoice" | "YesNo";

export type CustomQuestion = {
  text: string;
  type: QuestionType;
  options?: string[];
};

const MAX_QUESTION_TEXT_LENGTH = 200;
const MIN_QUESTION_TEXT_LENGTH = 3;
const MAX_OPTIONS = 5;
const MIN_OPTIONS = 2;

interface CustomPreBookingQuestionsSectionProps {
  questions: CustomQuestion[];
  onChange: (questions: CustomQuestion[]) => void;
  maxQuestions: number;
}

export function CustomPreBookingQuestionsSection({
  questions,
  onChange,
  maxQuestions
}: Readonly<CustomPreBookingQuestionsSectionProps>) {
  const isAtLimit = maxQuestions !== -1 && questions.length >= maxQuestions;

  const addQuestion = () => {
    onChange([...questions, { text: "", type: "Text" }]);
  };

  const updateQuestion = (index: number, updated: CustomQuestion) => {
    onChange(questions.map((question, i) => (i === index ? updated : question)));
  };

  const removeQuestion = (index: number) => {
    onChange(questions.filter((_, i) => i !== index));
  };

  return (
    <div className="flex flex-col gap-4">
      {questions.map((question, index) => (
        <CustomQuestionCard
          key={index}
          index={index}
          question={question}
          onChange={(updated) => updateQuestion(index, updated)}
          onRemove={() => removeQuestion(index)}
        />
      ))}
      <div className="flex flex-col gap-2">
        <div>
          <Button type="button" variant="outline" size="sm" onClick={addQuestion} disabled={isAtLimit}>
            <PlusIcon />
            <Trans>Add question</Trans>
          </Button>
        </div>
        {isAtLimit && maxQuestions > 0 && (
          <p className="text-sm text-muted-foreground">
            <Trans>You have reached the maximum of {maxQuestions} questions.</Trans>
          </p>
        )}
      </div>
    </div>
  );
}

interface CustomQuestionCardProps {
  index: number;
  question: CustomQuestion;
  onChange: (question: CustomQuestion) => void;
  onRemove: () => void;
}

function CustomQuestionCard({ index, question, onChange, onRemove }: Readonly<CustomQuestionCardProps>) {
  const [isTextTouched, setIsTextTouched] = useState(false);

  const textValidationErrors = buildTextValidationErrors(question.text, isTextTouched);
  const isTextInvalid = textValidationErrors.length > 0;
  const charCount = question.text.length;

  const handleTextChange = (value: string) => {
    onChange({ ...question, text: value });
  };

  const handleTypeChange = (newType: QuestionType) => {
    if (newType === "MultipleChoice") {
      onChange({
        ...question,
        type: newType,
        options: question.options?.length ? question.options : ["", ""]
      });
    } else {
      onChange({ ...question, type: newType, options: undefined });
    }
  };

  const updateOption = (optionIndex: number, value: string) => {
    const options = [...(question.options ?? [])];
    options[optionIndex] = value;
    onChange({ ...question, options });
  };

  const addOption = () => {
    if ((question.options?.length ?? 0) < MAX_OPTIONS) {
      onChange({ ...question, options: [...(question.options ?? []), ""] });
    }
  };

  const removeOption = (optionIndex: number) => {
    if ((question.options?.length ?? 0) > MIN_OPTIONS) {
      onChange({ ...question, options: question.options?.filter((_, i) => i !== optionIndex) });
    }
  };

  const inputId = `customQuestion_${index}_text`;
  const charCountId = `customQuestion_${index}_charCount`;

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-border bg-card p-4">
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-medium text-muted-foreground">
          <Trans>Question {index + 1}</Trans>
        </span>
        <Tooltip>
          <TooltipTrigger
            render={
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                onClick={onRemove}
                aria-label={t`Remove question ${index + 1}`}
              />
            }
          >
            <XIcon className="size-4" />
          </TooltipTrigger>
          <TooltipContent>
            <Trans>Remove question</Trans>
          </TooltipContent>
        </Tooltip>
      </div>

      <Field className="flex flex-col">
        <FieldLabel htmlFor={inputId}>
          <Trans>Question text</Trans>
        </FieldLabel>
        <Input
          id={inputId}
          value={question.text}
          onChange={(e) => handleTextChange(e.target.value)}
          onBlur={() => setIsTextTouched(true)}
          maxLength={MAX_QUESTION_TEXT_LENGTH}
          aria-invalid={isTextInvalid || undefined}
          aria-describedby={charCountId}
        />
        <div className="flex items-start justify-between gap-2">
          <FieldError errors={textValidationErrors} />
          <span id={charCountId} className="ml-auto shrink-0 text-xs text-muted-foreground" aria-live="polite">
            {charCount}/{MAX_QUESTION_TEXT_LENGTH}
          </span>
        </div>
      </Field>

      <Field className="flex flex-col">
        <FieldLabel>
          <Trans>Question type</Trans>
        </FieldLabel>
        <Select<QuestionType> value={question.type} onValueChange={(value) => value && handleTypeChange(value)}>
          <SelectTrigger>
            <SelectValue>
              {(value: string) => {
                const labels: Record<string, string> = {
                  Text: t`Short text answer`,
                  MultipleChoice: t`Multiple choice`,
                  YesNo: t`Yes / No`
                };
                return labels[value];
              }}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Text">
              <Trans>Short text answer</Trans>
            </SelectItem>
            <SelectItem value="MultipleChoice">
              <Trans>Multiple choice</Trans>
            </SelectItem>
            <SelectItem value="YesNo">
              <Trans>Yes / No</Trans>
            </SelectItem>
          </SelectContent>
        </Select>
      </Field>

      {question.type === "MultipleChoice" && (
        <div className="flex flex-col gap-2">
          {question.options?.map((option, optionIndex) => (
            <div key={optionIndex} className="flex items-center gap-2">
              <Input
                value={option}
                onChange={(e) => updateOption(optionIndex, e.target.value)}
                placeholder={t`Option ${optionIndex + 1}`}
                aria-label={t`Option ${optionIndex + 1} for question ${index + 1}`}
                className="flex-1"
              />
              <Tooltip>
                <TooltipTrigger
                  render={
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-xs"
                      onClick={() => removeOption(optionIndex)}
                      disabled={(question.options?.length ?? 0) <= MIN_OPTIONS}
                      aria-label={t`Remove option ${optionIndex + 1}`}
                    />
                  }
                >
                  <XIcon className="size-4" />
                </TooltipTrigger>
                <TooltipContent>
                  <Trans>Remove option</Trans>
                </TooltipContent>
              </Tooltip>
            </div>
          ))}
          {(question.options?.length ?? 0) < MAX_OPTIONS && (
            <Button type="button" variant="ghost" size="sm" onClick={addOption} className="w-fit">
              <PlusIcon />
              <Trans>Add option</Trans>
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

function buildTextValidationErrors(text: string, isTouched: boolean): Array<{ message: string }> {
  if (!isTouched) return [];
  if (!text.trim()) return [{ message: t`Question text is required.` }];
  if (text.trim().length < MIN_QUESTION_TEXT_LENGTH) {
    return [{ message: t`Question text must be at least ${MIN_QUESTION_TEXT_LENGTH} characters.` }];
  }
  return [];
}
