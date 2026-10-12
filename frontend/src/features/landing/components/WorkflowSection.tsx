import { workflowSteps } from '../data/content'
import { SectionHeading } from './SectionHeading'

export function WorkflowSection() {
  return (
    <section id="workflow" className="landing-section landing-workflow">
      <div className="landing-container">
        <SectionHeading eyebrow="CÁCH CODEPART HOẠT ĐỘNG" title="Một hành trình đơn giản." accent="Tiến bộ từng bước." description="Từ đọc đề đến hoàn thiện lời giải, mọi bước đều ở trong cùng một không gian." />
        <ol className="workflow-steps">
          {workflowSteps.map((step, index) => (
            <li key={step.number} data-reveal style={{ transitionDelay: `${index * 90}ms` }}>
              <span className="workflow-steps__number">{step.number}</span>
              <h3>{step.title}</h3>
              <p>{step.description}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
