import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { useBettingGroup } from '../context/BettingGroupContext';

interface FeedbackModalProps {
  onClose: () => void;
}

export default function FeedbackModal({ onClose }: FeedbackModalProps) {
  const { user } = useAuth();
  const { activeGroup } = useBettingGroup();
  const [message, setMessage] = useState('');
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') onClose();
  };

  const handleSubmit = async () => {
    if (!message.trim()) return;
    setSending(true);
    try {
      const webhookUrl = import.meta.env.VITE_SLACK_FEEDBACK_WEBHOOK;
      if (!webhookUrl) throw new Error('Webhook not configured');
      const slackText = `*${user?.name ?? 'Ukjent bruker'}* (liga: ${activeGroup?.name ?? 'ingen'})\n\n${message.trim()}`;
      await fetch(webhookUrl, {
        method: 'POST',
        body: JSON.stringify({ text: slackText }),
      });
      setSent(true);
      setTimeout(() => onClose(), 1500);
    } catch {
      alert('Noe gikk galt. Prøv igjen senere.');
    } finally {
      setSending(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
      onKeyDown={handleKeyDown}
    >
      <div
        className="mx-4 w-full max-w-md rounded-lg border p-6 shadow-xl"
        style={{ backgroundColor: 'var(--color-surface-card)', borderColor: 'var(--color-border)' }}
        onClick={e => e.stopPropagation()}
      >
        {sent ? (
          <p className="text-center text-sm font-medium" style={{ color: 'var(--color-text-primary)' }}>
            Takk for din tilbakemelding!
          </p>
        ) : (
          <>
            <p className="mb-4 text-sm" style={{ color: 'var(--color-text-muted)' }}>
              Her kan du komme med forslag eller melding om feil til bakrommet. Skriv inn det du måtte ønske og trykk send. Innsendte meldinger vil ikke bli besvart, men alle blir lest.
            </p>
            <textarea
              className="w-full rounded border p-2 text-sm"
              style={{
                backgroundColor: 'var(--color-surface-base)',
                borderColor: 'var(--color-border)',
                color: 'var(--color-text-primary)',
              }}
              rows={4}
              value={message}
              onChange={e => setMessage(e.target.value)}
              placeholder="Skriv din melding her..."
              autoFocus
            />
            <button
              onClick={handleSubmit}
              disabled={sending || !message.trim()}
              className="mt-3 w-full rounded px-4 py-2 text-sm font-medium text-white transition-opacity disabled:opacity-50"
              style={{ backgroundColor: 'var(--color-primary)' }}
            >
              {sending ? 'Sender...' : 'Send inn'}
            </button>
          </>
        )}
      </div>
    </div>
  );
}
