import React from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';

function BroadcastIcon() {
  return (
    <svg
      className={styles.featureSvg}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M4.9 4.9a10 10 0 0 1 14.2 0" />
      <path d="M7.5 7.5a6 6 0 0 1 9 0" />
      <path d="M12 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z" />
      <path d="M12 14v4" />
      <path d="M9 22h6" />
      <path d="M12 18v4" />
    </svg>
  );
}

function PipelineIcon() {
  return (
    <svg
      className={styles.featureSvg}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <rect x="3" y="3" width="4" height="4" rx="1" />
      <rect x="10" y="3" width="4" height="4" rx="1" />
      <rect x="17" y="3" width="4" height="4" rx="1" />
      <path d="M5 7v3a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7" />
      <path d="M12 12v3" />
      <rect x="10" y="17" width="4" height="4" rx="1" />
      <path d="M5 17h14" />
    </svg>
  );
}

function PuzzleIcon() {
  return (
    <svg
      className={styles.featureSvg}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M19.439 7.85c-.049.322.059.648.289.878l1.568 1.568c.47.47.706 1.087.706 1.704s-.235 1.233-.706 1.704l-1.611 1.611a.98.98 0 0 1-.837.276c-.47-.07-.802-.48-.968-.925a2.501 2.501 0 1 0-3.214 3.214c.446.166.855.497.925.968a.979.979 0 0 1-.276.837l-1.61 1.611a2.404 2.404 0 0 1-1.705.706 2.404 2.404 0 0 1-1.704-.706l-1.568-1.568a1.026 1.026 0 0 0-.877-.29c-.493.074-.84.504-1.02.968a2.5 2.5 0 1 1-3.237-3.237c.464-.18.894-.527.967-1.02a1.026 1.026 0 0 0-.289-.877l-1.568-1.568A2.404 2.404 0 0 1 1.998 12c0-.617.236-1.234.706-1.704L4.315 8.685a.98.98 0 0 1 .837-.276c.47.07.802.48.968.925a2.501 2.501 0 1 0 3.214-3.214c-.446-.166-.855-.497-.925-.968a.979.979 0 0 1 .276-.837l1.61-1.611a2.404 2.404 0 0 1 1.705-.706c.617 0 1.234.236 1.704.706l1.568 1.568c.23.23.556.338.877.29.493-.074.84-.504 1.02-.969a2.5 2.5 0 1 1 3.237 3.237c-.464.18-.894.527-.967 1.02Z" />
    </svg>
  );
}

type FeatureItem = {
  title: string;
  Icon: React.ComponentType;
  description: React.JSX.Element;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'CloudEvents-Native',
    Icon: BroadcastIcon,
    description: (
      <>
        Built on the CNCF CloudEvents standard &mdash; schema-first contracts,
        AsyncAPI export, multi-transport delivery. Every event carries the
        canonical envelope out of the box.
      </>
    ),
  },
  {
    title: 'Pluggable Publisher Pipeline',
    Icon: PipelineIcon,
    description: (
      <>
        Middleware, outbox, dead-letter capture, delivery log &mdash; compose
        cross-cutting concerns with zero boilerplate. Swap transports without
        changing application code.
      </>
    ),
  },
  {
    title: 'Extensible & Testable',
    Icon: PuzzleIcon,
    description: (
      <>
        In-memory test channels, framework integrations (MediatR, Wolverine,
        MassTransit), and a subscription routing engine &mdash; build for
        real-world systems without vendor lock-in.
      </>
    ),
  },
];

function Feature({ title, Icon, description }: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <Icon />
      </div>
      <div className="text--center padding-horiz--md">
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): React.JSX.Element {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
