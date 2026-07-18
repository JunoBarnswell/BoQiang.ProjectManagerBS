import { ImWorkspace } from '@/features/im/components/ImWorkspace';
import { ProjectManagementImConversationTargetLink } from '@/features/project-management/im/ProjectManagementImConversationTargetLink';

export default function ImMessagesPage() {
  return <ImWorkspace mode="full" renderConversationContext={(conversation) => conversation.conversationType === 'Group' && conversation.conversationKey.startsWith('pm:')
    ? <ProjectManagementImConversationTargetLink conversationId={conversation.id} />
    : null}
  />;
}
